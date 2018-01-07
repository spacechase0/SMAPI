using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.Exceptions;
using StardewModdingAPI.Metadata;

namespace StardewModdingAPI.Framework.ModLoading
{
    /// <summary>Preprocesses and loads mod assemblies.</summary>
    internal class AssemblyLoader
    {
        /*********
        ** Properties
        *********/
        /// <summary>Metadata for mapping assemblies to the current platform.</summary>
        private readonly PlatformAssemblyMap AssemblyMap;

        /// <summary>A type => assembly lookup for types which should be rewritten.</summary>
        private readonly IDictionary<string, Assembly> TypeAssemblies;

        /// <summary>Encapsulates monitoring and logging.</summary>
        private readonly IMonitor Monitor;

        /// <summary>Whether to enable developer mode logging.</summary>
        private readonly bool IsDeveloperMode;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="targetPlatform">The current game platform.</param>
        /// <param name="monitor">Encapsulates monitoring and logging.</param>
        /// <param name="isDeveloperMode">Whether to enable developer mode logging.</param>
        public AssemblyLoader(Platform targetPlatform, IMonitor monitor, bool isDeveloperMode)
        {
            this.Monitor = monitor;
            this.IsDeveloperMode = isDeveloperMode;
            this.AssemblyMap = Constants.GetAssemblyMap(targetPlatform);

            // generate type => assembly lookup for types which should be rewritten
            this.TypeAssemblies = new Dictionary<string, Assembly>();
            foreach (Assembly assembly in this.AssemblyMap.Targets)
            {
                ModuleDefinition module = this.AssemblyMap.TargetModules[assembly];
                foreach (TypeDefinition type in module.GetTypes())
                {
                    if (!type.IsPublic)
                        continue; // no need to rewrite
                    if (type.Namespace.Contains("<"))
                        continue; // ignore assembly metadata
                    this.TypeAssemblies[type.FullName] = assembly;
                }
            }
        }

        /// <summary>Preprocess and load an assembly.</summary>
        /// <param name="mod">The mod for which the assembly is being loaded.</param>
        /// <param name="assemblyPath">The assembly file path.</param>
        /// <param name="assumeCompatible">Assume the mod is compatible, even if incompatible code is detected.</param>
        /// <returns>Returns the rewrite metadata for the preprocessed assembly.</returns>
        /// <exception cref="IncompatibleInstructionException">An incompatible CIL instruction was found while rewriting the assembly.</exception>
        public Assembly Load(IModMetadata mod, string assemblyPath, bool assumeCompatible)
        {
            // get referenced local assemblies
            AssemblyParseResult[] assemblies;
            {
                AssemblyDefinitionResolver resolver = new AssemblyDefinitionResolver();
                HashSet<string> visitedAssemblyNames = new HashSet<string>(AppDomain.CurrentDomain.GetAssemblies().Select(p => p.GetName().Name)); // don't try loading assemblies that are already loaded
                assemblies = this.GetReferencedLocalAssemblies(new FileInfo(assemblyPath), visitedAssemblyNames, resolver).ToArray();
            }

            // validate load
            if (!assemblies.Any() || assemblies[0].Status == AssemblyLoadStatus.Failed)
            {
                throw new SAssemblyLoadFailedException(!File.Exists(assemblyPath)
                    ? $"Could not load '{assemblyPath}' because it doesn't exist."
                    : $"Could not load '{assemblyPath}'."
                );
            }
            if (assemblies.Last().Status == AssemblyLoadStatus.AlreadyLoaded) // mod assembly is last in dependency order
                throw new SAssemblyLoadFailedException($"Could not load '{assemblyPath}' because it was already loaded. Do you have two copies of this mod?");

            // rewrite & load assemblies in leaf-to-root order
            bool oneAssembly = assemblies.Length == 1;
            Assembly lastAssembly = null;
            HashSet<string> loggedMessages = new HashSet<string>();
            foreach (AssemblyParseResult assembly in assemblies)
            {
                if (assembly.Status == AssemblyLoadStatus.AlreadyLoaded)
                    continue;

                bool changed = this.RewriteAssembly(mod, assembly.Definition, assumeCompatible, loggedMessages, logPrefix: "   ");
                if (changed)
                {
                    if (!oneAssembly)
                        this.Monitor.Log($"   Loading {assembly.File.Name} (rewritten in memory)...", LogLevel.Trace);
                    using (MemoryStream outStream = new MemoryStream())
                    {
                        assembly.Definition.Write(outStream);
                        byte[] bytes = outStream.ToArray();
                        lastAssembly = Assembly.Load(bytes);
                        FileStream fs = new FileStream("out.dll",FileMode.CreateNew);
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Close();
                    }
                }
                else
                {
                    if (!oneAssembly)
                        this.Monitor.Log($"   Loading {assembly.File.Name}...", LogLevel.Trace);
                    lastAssembly = Assembly.UnsafeLoadFrom(assembly.File.FullName);
                }
            }

            // last assembly loaded is the root
            return lastAssembly;
        }

        /// <summary>Resolve an assembly by its name.</summary>
        /// <param name="name">The assembly name.</param>
        /// <remarks>
        /// This implementation returns the first loaded assembly which matches the short form of
        /// the assembly name, to resolve assembly resolution issues when rewriting
        /// assemblies (especially with Mono). Since this is meant to be called on <see cref="AppDomain.AssemblyResolve"/>,
        /// the implicit assumption is that loading the exact assembly failed.
        /// </remarks>
        public Assembly ResolveAssembly(string name)
        {
            string shortName = name.Split(new[] { ',' }, 2).First(); // get simple name (without version and culture)
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(p => p.GetName().Name == shortName);
        }


        /*********
        ** Private methods
        *********/
        /****
        ** Assembly parsing
        ****/
        /// <summary>Get a list of referenced local assemblies starting from the mod assembly, ordered from leaf to root.</summary>
        /// <param name="file">The assembly file to load.</param>
        /// <param name="visitedAssemblyNames">The assembly names that should be skipped.</param>
        /// <param name="assemblyResolver">A resolver which resolves references to known assemblies.</param>
        /// <returns>Returns the rewrite metadata for the preprocessed assembly.</returns>
        private IEnumerable<AssemblyParseResult> GetReferencedLocalAssemblies(FileInfo file, HashSet<string> visitedAssemblyNames, IAssemblyResolver assemblyResolver)
        {
            // validate
            if (file.Directory == null)
                throw new InvalidOperationException($"Could not get directory from file path '{file.FullName}'.");
            if (!file.Exists)
                yield break; // not a local assembly

            // read assembly
            byte[] assemblyBytes = File.ReadAllBytes(file.FullName);
            AssemblyDefinition assembly;
            using (Stream readStream = new MemoryStream(assemblyBytes))
                assembly = AssemblyDefinition.ReadAssembly(readStream, new ReaderParameters(ReadingMode.Deferred) { AssemblyResolver = assemblyResolver });

            // skip if already visited
            if (visitedAssemblyNames.Contains(assembly.Name.Name))
            {
                yield return new AssemblyParseResult(file, null, AssemblyLoadStatus.AlreadyLoaded);
                yield break;
            }

            visitedAssemblyNames.Add(assembly.Name.Name);

            // yield referenced assemblies
            foreach (AssemblyNameReference dependency in assembly.MainModule.AssemblyReferences)
            {
                FileInfo dependencyFile = new FileInfo(Path.Combine(file.Directory.FullName, $"{dependency.Name}.dll"));
                foreach (AssemblyParseResult result in this.GetReferencedLocalAssemblies(dependencyFile, visitedAssemblyNames, assemblyResolver))
                    yield return result;
            }

            // yield assembly
            yield return new AssemblyParseResult(file, assembly, AssemblyLoadStatus.Okay);
        }

        void func(Mono.Cecil.CustomAttributeArgument c)
        {
            if (c.Type.FullName == "System.Type")
                ChangeTypeScope((TypeReference)c.Value);
        }
        void func(Mono.Cecil.CustomAttributeNamedArgument c)
        {
            func(c.Argument);
        }
        void func(Mono.Cecil.CustomAttribute c)
        {
            foreach (var a in c.ConstructorArguments)
                func(a);
            foreach (var f in c.Fields)
                func(f);
        }
        void func(Mono.Cecil.FieldDefinition f)
        {
            foreach (var c in f.CustomAttributes)
                func(c);
        }
        void func(Mono.Cecil.ParameterDefinition m)
        {
            foreach (var c in m.CustomAttributes)
                func(c);
        }
        void func(Mono.Cecil.MethodDefinition m)
        {
            foreach (var c in m.CustomAttributes)
                func(c);
            foreach (var p in m.Parameters)
                func(p);
        }
        void func(Mono.Cecil.PropertyDefinition p)
        {
            foreach (var c in p.CustomAttributes)
                func(c);
            func(p.GetMethod);
            func(p.SetMethod);
        }
        void func(Mono.Cecil.EventDefinition e)
        {
            foreach (var c in e.CustomAttributes)
                func(c);
        }
        void func(Mono.Cecil.TypeDefinition t)
        {
            foreach (var c in t.CustomAttributes)
                func(c);
            foreach (var f in t.Fields)
                func(f);
            foreach (var m in t.Methods)
                func(m);
            foreach (var p in t.Properties)
                func(p);
            foreach (var e in t.Events)
                func(e);
            foreach (var n in t.NestedTypes)
                func(n);
        }
        void func(Mono.Cecil.ModuleDefinition m)
        {
            foreach (var t in m.Types)
                func(t);
        }

        /****
        ** Assembly rewriting
        ****/
        /// <summary>Rewrite the types referenced by an assembly.</summary>
        /// <param name="mod">The mod for which the assembly is being loaded.</param>
        /// <param name="assembly">The assembly to rewrite.</param>
        /// <param name="assumeCompatible">Assume the mod is compatible, even if incompatible code is detected.</param>
        /// <param name="loggedMessages">The messages that have already been logged for this mod.</param>
        /// <param name="logPrefix">A string to prefix to log messages.</param>
        /// <returns>Returns whether the assembly was modified.</returns>
        /// <exception cref="IncompatibleInstructionException">An incompatible CIL instruction was found while rewriting the assembly.</exception>
        private bool RewriteAssembly(IModMetadata mod, AssemblyDefinition assembly, bool assumeCompatible, HashSet<string> loggedMessages, string logPrefix)
        {
            ModuleDefinition module = assembly.MainModule;
            string filename = $"{assembly.Name.Name}.dll";

            // swap assembly references if needed (e.g. XNA => MonoGame)
            bool platformChanged = false;
            for (int i = 0; i < module.AssemblyReferences.Count; i++)
            {
                // remove old assembly reference
                if (this.AssemblyMap.RemoveNames.Any(name => module.AssemblyReferences[i].Name == name))
                {
                    this.Monitor.LogOnce(loggedMessages, $"{logPrefix}Rewriting {filename} for OS...");
                    platformChanged = true;
                    module.AssemblyReferences.RemoveAt(i);
                    i--;
                }
            }
            if (platformChanged)
            {
                // add target assembly references
                foreach (AssemblyNameReference target in this.AssemblyMap.TargetReferences.Values)
                    module.AssemblyReferences.Add(target);

                // rewrite type scopes to use target assemblies
                IEnumerable<TypeReference> typeReferences = module.GetTypeReferences().OrderBy(p => p.FullName);
                foreach (TypeReference type in typeReferences)
                    this.ChangeTypeScope(type);

                func(module);
            }

            // find (and optionally rewrite) incompatible instructions
            bool anyRewritten = false;
            IInstructionHandler[] handlers = new InstructionMetadata().GetHandlers().ToArray();
            foreach (MethodDefinition method in this.GetMethods(module))
            {
                // check method definition
                foreach (IInstructionHandler handler in handlers)
                {
                    InstructionHandleResult result = handler.Handle(module, method, this.AssemblyMap, platformChanged);
                    this.ProcessInstructionHandleResult(mod, handler, result, loggedMessages, logPrefix, assumeCompatible, filename);
                    if (result == InstructionHandleResult.Rewritten)
                        anyRewritten = true;
                }

                // check CIL instructions
                ILProcessor cil = method.Body.GetILProcessor();
                foreach (Instruction instruction in cil.Body.Instructions.ToArray())
                {
                    foreach (IInstructionHandler handler in handlers)
                    {
                        InstructionHandleResult result = handler.Handle(module, cil, instruction, this.AssemblyMap, platformChanged);
                        this.ProcessInstructionHandleResult(mod, handler, result, loggedMessages, logPrefix, assumeCompatible, filename);
                        if (result == InstructionHandleResult.Rewritten)
                            anyRewritten = true;
                    }
                }
            }

            return platformChanged || anyRewritten;
        }

        /// <summary>Process the result from an instruction handler.</summary>
        /// <param name="mod">The mod being analysed.</param>
        /// <param name="handler">The instruction handler.</param>
        /// <param name="result">The result returned by the handler.</param>
        /// <param name="loggedMessages">The messages already logged for the current mod.</param>
        /// <param name="assumeCompatible">Assume the mod is compatible, even if incompatible code is detected.</param>
        /// <param name="logPrefix">A string to prefix to log messages.</param>
        /// <param name="filename">The assembly filename for log messages.</param>
        private void ProcessInstructionHandleResult(IModMetadata mod, IInstructionHandler handler, InstructionHandleResult result, HashSet<string> loggedMessages, string logPrefix, bool assumeCompatible, string filename)
        {
            switch (result)
            {
                case InstructionHandleResult.Rewritten:
                    this.Monitor.LogOnce(loggedMessages, $"{logPrefix}Rewrote {filename} to fix {handler.NounPhrase}...");
                    break;

                case InstructionHandleResult.NotCompatible:
                    if (!assumeCompatible)
                        throw new IncompatibleInstructionException(handler.NounPhrase, $"Found an incompatible CIL instruction ({handler.NounPhrase}) while loading assembly {filename}.");
                    this.Monitor.LogOnce(loggedMessages, $"{logPrefix}Found an incompatible CIL instruction ({handler.NounPhrase}) while loading assembly {filename}, but SMAPI is configured to allow it anyway. The mod may crash or behave unexpectedly.", LogLevel.Warn);
                    break;

                case InstructionHandleResult.DetectedGamePatch:
                    this.Monitor.LogOnce(loggedMessages, $"{logPrefix}Detected game patcher ({handler.NounPhrase}) in assembly {filename}.");
                    this.Monitor.LogOnce(loggedMessages, $"{mod.DisplayName} patches the game, which may impact game stability. If you encounter problems, try removing this mod first.", LogLevel.Warn);
                    break;

                case InstructionHandleResult.DetectedSaveSerialiser:
                    this.Monitor.LogOnce(loggedMessages, $"{logPrefix}Detected possible save serialiser change ({handler.NounPhrase}) in assembly {filename}.");
                    this.Monitor.LogOnce(loggedMessages, $"{mod.DisplayName} seems to change the save serialiser. It may change your saves in such a way that they won't work without this mod in the future.", LogLevel.Warn);
                    break;

                case InstructionHandleResult.DetectedDynamic:
                    this.Monitor.LogOnce(loggedMessages, $"{logPrefix}Detected 'dynamic' keyword ({handler.NounPhrase}) in assembly {filename}.");
                    this.Monitor.LogOnce(loggedMessages, $"{mod.DisplayName} uses the 'dynamic' keyword, which isn't compatible with Stardew Valley on Linux or Mac.",
#if SMAPI_FOR_WINDOWS
                        this.IsDeveloperMode ? LogLevel.Warn : LogLevel.Debug
#else
                        LogLevel.Warn
#endif
                    );
                    break;

                case InstructionHandleResult.None:
                    break;

                default:
                    throw new NotSupportedException($"Unrecognised instruction handler result '{result}'.");
            }
        }

        /// <summary>Get the correct reference to use for compatibility with the current platform.</summary>
        /// <param name="type">The type reference to rewrite.</param>
        private void ChangeTypeScope(TypeReference type)
        {
            // check skip conditions
            if (type == null || type.FullName.StartsWith("System."))
                return;

            // get assembly
            if (!this.TypeAssemblies.TryGetValue(type.FullName, out Assembly assembly))
                return;

            // replace scope
            AssemblyNameReference assemblyRef = this.AssemblyMap.TargetReferences[assembly];
            type.Scope = assemblyRef;
        }

        /// <summary>Get all methods in a module.</summary>
        /// <param name="module">The module to search.</param>
        private IEnumerable<MethodDefinition> GetMethods(ModuleDefinition module)
        {
            return (
                from type in module.GetTypes()
                where type.HasMethods
                from method in type.Methods
                where method.HasBody
                select method
            );
        }
    }
}
