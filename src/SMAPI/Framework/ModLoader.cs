using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using StardewModdingAPI.Events;
using StardewModdingAPI.Framework.Events;
using StardewModdingAPI.Framework.Exceptions;
using StardewModdingAPI.Framework.Input;
using StardewModdingAPI.Framework.Models;
using StardewModdingAPI.Framework.ModHelpers;
using StardewModdingAPI.Framework.ModLoading;
using StardewModdingAPI.Framework.Reflection;
using StardewModdingAPI.Toolkit;
using StardewModdingAPI.Toolkit.Framework.Clients.WebApi;
using StardewModdingAPI.Toolkit.Framework.ModData;
using StardewModdingAPI.Toolkit.Serialization;

namespace StardewModdingAPI.Framework
{
    internal interface IAssetIntercepter
    {
        void OnInterceptorsChanged( IModMetadata mod, IEnumerable<IAssetEditor> added, IEnumerable<IAssetEditor> removed, IList<ModLinked<IAssetEditor>> list);
        void OnInterceptorsChanged( IModMetadata mod, IEnumerable<IAssetLoader> added, IEnumerable<IAssetLoader> removed, IList<ModLinked<IAssetLoader>> list);
        void OnAssetInterceptorsChanged( IModMetadata mod, System.Collections.IList newItems, System.Collections.IList oldItems );
    }

    internal class ModInitStuff
    {
        public ContentCoordinator contentCore;
        public EventManager eventManager;
        public CommandManager commandManager;
        public SMultiplayer multiplayer;
        public Reflector reflection;
        public SInputState input;
    }

    internal class ModLoader
    {
        private readonly Monitor Monitor;
        private readonly ModRegistry ModRegistry;
        private readonly string ModsPath;
        private readonly ModToolkit Toolkit;
        private readonly SConfig Settings;
        private readonly Func<string, Monitor> GetModMonitor;

        public ModLoader(Monitor monitor, ModRegistry registry, string modsPath, ModToolkit toolkit, SConfig settings, Func<string, Monitor> getModMonitor)
        {
            this.Monitor = monitor;
            this.ModRegistry = registry;
            this.ModsPath = modsPath;
            this.Toolkit = toolkit;
            this.Settings = settings;
            this.GetModMonitor = getModMonitor;
        }

        public void PreLoadMods()
        {
            // load mod data
            ModDatabase modDatabase = this.Toolkit.GetModDatabase(Constants.ApiMetadataPath);

            // load mods
            {
                this.Monitor.Log( "Loading mod metadata...", LogLevel.Trace );
                ModResolver resolver = new ModResolver();

                // log loose files
                {
                    string[] looseFiles = new DirectoryInfo(this.ModsPath).GetFiles().Select(p => p.Name).ToArray();
                    if ( looseFiles.Any() )
                        this.Monitor.Log( $"  Ignored loose files: {string.Join( ", ", looseFiles.OrderBy( p => p, StringComparer.InvariantCultureIgnoreCase ) )}", LogLevel.Trace );
                }

                // load manifests
                IModMetadata[] mods = resolver.ReadManifests(this.Toolkit, this.ModsPath, modDatabase).ToArray();

                // filter out ignored mods
                foreach ( IModMetadata mod in mods.Where( p => p.IsIgnored ) )
                    this.Monitor.Log( $"  Skipped {mod.GetRelativePathWithRoot()} (folder name starts with a dot).", LogLevel.Trace );
                mods = mods.Where( p => !p.IsIgnored ).ToArray();

                // load mods
                resolver.ValidateManifests( mods, Constants.ApiVersion, this.Toolkit.GetUpdateUrl );
                mods = resolver.ProcessDependencies( mods, modDatabase ).ToArray();
                this.PreLoadMods( mods, this.Toolkit.JsonHelper, modDatabase );
            }
        }

        public void LoadMods(ModInitStuff initStuff, Action<IModMetadata[]> reloadTranslations, IAssetIntercepter assetInterceptor)
        {
            this.LoadMods( this.ModRegistry, this.Toolkit.JsonHelper, initStuff, this.Toolkit.GetModDatabase( Constants.ApiMetadataPath ), reloadTranslations, assetInterceptor );
            
            // check for updates
            this.CheckForUpdatesAsync( this.ModRegistry.GetAll().ToArray() );
        }

        /// <summary>Asynchronously check for a new version of SMAPI and any installed mods, and print alerts to the console if an update is available.</summary>
        /// <param name="mods">The mods to include in the update check (if eligible).</param>
        private void CheckForUpdatesAsync( IModMetadata[] mods )
        {
            if ( !this.Settings.CheckForUpdates )
                return;

            new Thread( () =>
            {
                // create client
                string url = this.Settings.WebApiBaseUrl;
#if !SMAPI_FOR_WINDOWS
                url = url.Replace("https://", "http://"); // workaround for OpenSSL issues with the game's bundled Mono on Linux/Mac
#endif
                WebApiClient client = new WebApiClient(url, Constants.ApiVersion);
                this.Monitor.Log( "Checking for updates...", LogLevel.Trace );

                // check SMAPI version
                ISemanticVersion updateFound = null;
                try
                {
                    // fetch update check
                    ModEntryModel response = client.GetModInfo(new[] { new ModSearchEntryModel("Pathoschild.SMAPI", Constants.ApiVersion, new[] { $"GitHub:{this.Settings.GitHubProjectName}" }) }, apiVersion: Constants.ApiVersion, gameVersion: GameConstants.GameVersion, platform: Constants.Platform).Single().Value;
                    if ( response.SuggestedUpdate != null )
                        this.Monitor.Log( $"You can update SMAPI to {response.SuggestedUpdate.Version}: {Constants.HomePageUrl}", LogLevel.Alert );
                    else
                        this.Monitor.Log( "   SMAPI okay.", LogLevel.Trace );

                    updateFound = response.SuggestedUpdate?.Version;

                    // show errors
                    if ( response.Errors.Any() )
                    {
                        this.Monitor.Log( "Couldn't check for a new version of SMAPI. This won't affect your game, but you may not be notified of new versions if this keeps happening.", LogLevel.Warn );
                        this.Monitor.Log( $"Error: {string.Join( "\n", response.Errors )}", LogLevel.Trace );
                    }
                }
                catch ( Exception ex )
                {
                    this.Monitor.Log( "Couldn't check for a new version of SMAPI. This won't affect your game, but you won't be notified of new versions if this keeps happening.", LogLevel.Warn );
                    this.Monitor.Log( ex is WebException && ex.InnerException == null
                        ? $"Error: {ex.Message}"
                        : $"Error: {ex.GetLogSummary()}", LogLevel.Trace
                    );
                }

                // show update message on next launch
                if ( updateFound != null )
                    File.WriteAllText( Constants.UpdateMarker, updateFound.ToString() );

                // check mod versions
                if ( mods.Any() )
                {
                    try
                    {
                        HashSet<string> suppressUpdateChecks = new HashSet<string>(this.Settings.SuppressUpdateChecks, StringComparer.InvariantCultureIgnoreCase);

                        // prepare search model
                        List<ModSearchEntryModel> searchMods = new List<ModSearchEntryModel>();
                        foreach ( IModMetadata mod in mods )
                        {
                            if ( !mod.HasID() || suppressUpdateChecks.Contains( mod.Manifest.UniqueID ) )
                                continue;

                            string[] updateKeys = mod
                                .GetUpdateKeys(validOnly: true)
                                .Select(p => p.ToString())
                                .ToArray();
                            searchMods.Add( new ModSearchEntryModel( mod.Manifest.UniqueID, mod.Manifest.Version, updateKeys.ToArray(), isBroken: mod.Status == ModMetadataStatus.Failed ) );
                        }

                        // fetch results
                        this.Monitor.Log( $"   Checking for updates to {searchMods.Count} mods...", LogLevel.Trace );
                        IDictionary<string, ModEntryModel> results = client.GetModInfo(searchMods.ToArray(), apiVersion: Constants.ApiVersion, gameVersion: GameConstants.GameVersion, platform: Constants.Platform);

                        // extract update alerts & errors
                        var updates = new List<Tuple<IModMetadata, ISemanticVersion, string>>();
                        var errors = new StringBuilder();
                        foreach ( IModMetadata mod in mods.OrderBy( p => p.DisplayName ) )
                        {
                            // link to update-check data
                            if ( !mod.HasID() || !results.TryGetValue( mod.Manifest.UniqueID, out ModEntryModel result ) )
                                continue;
                            mod.SetUpdateData( result );

                            // handle errors
                            if ( result.Errors != null && result.Errors.Any() )
                            {
                                errors.AppendLine( result.Errors.Length == 1
                                    ? $"   {mod.DisplayName}: {result.Errors[ 0 ]}"
                                    : $"   {mod.DisplayName}:\n      - {string.Join( "\n      - ", result.Errors )}"
                                );
                            }

                            // handle update
                            if ( result.SuggestedUpdate != null )
                                updates.Add( Tuple.Create( mod, result.SuggestedUpdate.Version, result.SuggestedUpdate.Url ) );
                        }

                        // show update errors
                        if ( errors.Length != 0 )
                            this.Monitor.Log( "Got update-check errors for some mods:\n" + errors.ToString().TrimEnd(), LogLevel.Trace );

                        // show update alerts
                        if ( updates.Any() )
                        {
                            this.Monitor.Newline();
                            this.Monitor.Log( $"You can update {updates.Count} mod{( updates.Count != 1 ? "s" : "" )}:", LogLevel.Alert );
                            foreach ( var entry in updates )
                            {
                                IModMetadata mod = entry.Item1;
                                ISemanticVersion newVersion = entry.Item2;
                                string newUrl = entry.Item3;
                                this.Monitor.Log( $"   {mod.DisplayName} {newVersion}: {newUrl}", LogLevel.Alert );
                            }
                        }
                        else
                            this.Monitor.Log( "   All mods up to date.", LogLevel.Trace );
                    }
                    catch ( Exception ex )
                    {
                        this.Monitor.Log( "Couldn't check for new mod versions. This won't affect your game, but you won't be notified of mod updates if this keeps happening.", LogLevel.Warn );
                        this.Monitor.Log( ex is WebException && ex.InnerException == null
                            ? ex.Message
                            : ex.ToString(), LogLevel.Trace
                        );
                    }
                }
            } ).Start();
        }


        /// <summary>Write a summary of mod warnings to the console and log.</summary>
        /// <param name="mods">The loaded mods.</param>
        /// <param name="skippedMods">The mods which were skipped, along with the friendly and developer reasons.</param>
        private void LogModWarnings( IEnumerable<IModMetadata> mods, IDictionary<IModMetadata, Tuple<string, string>> skippedMods )
        {
            // get mods with warnings
            IModMetadata[] modsWithWarnings = mods.Where(p => p.Warnings != ModWarning.None).ToArray();
            if ( !modsWithWarnings.Any() && !skippedMods.Any() )
                return;

            // log intro
            {
                int count = modsWithWarnings.Union(skippedMods.Keys).Count();
                this.Monitor.Log( $"Found {count} mod{( count == 1 ? "" : "s" )} with warnings:", LogLevel.Info );
            }

            // log skipped mods
            if ( skippedMods.Any() )
            {
                // get logging logic
                HashSet<string> logged = new HashSet<string>();
                void LogSkippedMod( IModMetadata mod, string errorReason, string errorDetails )
                {
                    string message = $"      - {mod.DisplayName}{(mod.Manifest?.Version != null ? " " + mod.Manifest.Version.ToString() : "")} because {errorReason}";

                    if ( logged.Add( $"{message}|{errorDetails}" ) )
                    {
                        this.Monitor.Log( message, LogLevel.Error );
                        if ( errorDetails != null )
                            this.Monitor.Log( $"        ({errorDetails})", LogLevel.Trace );
                    }
                }

                // find skipped dependencies
                KeyValuePair<IModMetadata, Tuple<string, string>>[] skippedDependencies;
                {
                    HashSet<string> skippedDependencyIds = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    HashSet<string> skippedModIds = new HashSet<string>(from mod in skippedMods where mod.Key.HasID() select mod.Key.Manifest.UniqueID, StringComparer.InvariantCultureIgnoreCase);
                    foreach ( IModMetadata mod in skippedMods.Keys )
                    {
                        foreach ( string requiredId in skippedModIds.Intersect( mod.GetRequiredModIds() ) )
                            skippedDependencyIds.Add( requiredId );
                    }
                    skippedDependencies = skippedMods.Where( p => p.Key.HasID() && skippedDependencyIds.Contains( p.Key.Manifest.UniqueID ) ).ToArray();
                }

                // log skipped mods
                this.Monitor.Log( "   Skipped mods", LogLevel.Error );
                this.Monitor.Log( "   " + "".PadRight( 50, '-' ), LogLevel.Error );
                this.Monitor.Log( "      These mods could not be added to your game.", LogLevel.Error );
                this.Monitor.Newline();

                if ( skippedDependencies.Any() )
                {
                    foreach ( var pair in skippedDependencies.OrderBy( p => p.Key.DisplayName ) )
                        LogSkippedMod( pair.Key, pair.Value.Item1, pair.Value.Item2 );
                    this.Monitor.Newline();
                }

                foreach ( var pair in skippedMods.OrderBy( p => p.Key.DisplayName ) )
                    LogSkippedMod( pair.Key, pair.Value.Item1, pair.Value.Item2 );
                this.Monitor.Newline();
            }

            // log warnings
            if ( modsWithWarnings.Any() )
            {
                // broken code
                this.LogModWarningGroup( modsWithWarnings, ModWarning.BrokenCodeLoaded, LogLevel.Error, "Broken mods",
                     new string[] {"These mods have broken code, but you configured SMAPI to load them anyway. This may cause bugs,",
                    "errors, or crashes in-game." }
                );

                // changes serializer
                this.LogModWarningGroup( modsWithWarnings, ModWarning.ChangesSaveSerializer, LogLevel.Warn, "Changed save serializer",
                     new string[] {"These mods change the save serializer. They may corrupt your save files, or make them unusable if",
                    "you uninstall these mods." }
                );

                // edits game code game code
                this.LogModWarningGroup( modsWithWarnings, ModWarning.EditsGame, LogLevel.Warn, "Edits game code",
                     new string[] {"These mods directly change the game code. They're more likely to cause errors or bugs in-game; if",
                    "your game has issues, try removing these first. Otherwise you can ignore this warning." }
                );

                // patched game code
                this.LogModWarningGroup( modsWithWarnings, ModWarning.PatchesGame, LogLevel.Info, "Patched game code",
                     new string[] {"These mods patch the game code. They're more likely to cause errors or bugs in-game; if",
                    "your game has issues, try removing these first. Otherwise you can ignore this warning." }
                );

                // unvalidated update tick
                this.LogModWarningGroup( modsWithWarnings, ModWarning.UsesUnvalidatedUpdateTick, LogLevel.Info, "Bypassed safety checks",
                     new string[] {"These mods bypass SMAPI's normal safety checks, so they're more likely to cause errors or save",
                    "corruption. If your game has issues, try removing these first." }
                );

                // paranoid warnings
                if ( this.Settings.ParanoidWarnings )
                {
                    this.LogModWarningGroup(
                        modsWithWarnings,
                        match: mod => mod.HasUnsuppressedWarnings( ModWarning.AccessesConsole, ModWarning.AccessesFilesystem, ModWarning.AccessesShell ),
                        level: LogLevel.Debug,
                        heading: "Direct system access",
                        blurb: new[]
                        {
                            "You enabled paranoid warnings and these mods directly access the filesystem, shells/processes, or",
                            "SMAPI console. (This is usually legitimate and innocent usage; this warning is only useful for",
                            "further investigation.)"
                        },
                        modLabel: mod =>
                        {
                            List<string> labels = new List<string>();
                            if ( mod.HasUnsuppressedWarnings( ModWarning.AccessesConsole ) )
                                labels.Add( "console" );
                            if ( mod.HasUnsuppressedWarnings( ModWarning.AccessesFilesystem ) )
                                labels.Add( "files" );
                            if ( mod.HasUnsuppressedWarnings( ModWarning.AccessesShell ) )
                                labels.Add( "shells/processes" );

                            return $"{mod.DisplayName} ({string.Join( ", ", labels )})";
                        }
                    );
                }

                // no update keys
                this.LogModWarningGroup( modsWithWarnings, ModWarning.NoUpdateKeys, LogLevel.Debug, "No update keys",
                     new string[] {"These mods have no update keys in their manifest. SMAPI may not notify you about updates for these",
                    "mods. Consider notifying the mod authors about this problem." }
                );

                // not crossplatform
                this.LogModWarningGroup( modsWithWarnings, ModWarning.UsesDynamic, LogLevel.Debug, "Not crossplatform",
                     new string[] { "These mods use the 'dynamic' keyword, and won't work on Linux/Mac." }
                );
            }
        }

        /// <summary>Write a mod warning group to the console and log.</summary>
        /// <param name="mods">The mods to search.</param>
        /// <param name="match">Matches mods to include in the warning group.</param>
        /// <param name="level">The log level for the logged messages.</param>
        /// <param name="heading">A brief heading label for the group.</param>
        /// <param name="blurb">A detailed explanation of the warning, split into lines.</param>
        /// <param name="modLabel">Formats the mod label, or <c>null</c> to use the <see cref="IModMetadata.DisplayName"/>.</param>
        private void LogModWarningGroup( IModMetadata[] mods, Func<IModMetadata, bool> match, LogLevel level, string heading, string[] blurb, Func<IModMetadata, string> modLabel = null )
        {
            // get matching mods
            string[] modLabels = mods
                .Where(match)
                .Select(mod => modLabel?.Invoke(mod) ?? mod.DisplayName)
                .OrderBy(p => p)
                .ToArray();
            if ( !modLabels.Any() )
                return;

            // log header/blurb
            this.Monitor.Log( "   " + heading, level );
            this.Monitor.Log( "   " + "".PadRight( 50, '-' ), level );
            foreach ( string line in blurb )
                this.Monitor.Log( "      " + line, level );
            this.Monitor.Newline();

            // log mod list
            foreach ( string label in modLabels )
                this.Monitor.Log( $"      - {label}", level );

            this.Monitor.Newline();
        }

        /// <summary>Write a mod warning group to the console and log.</summary>
        /// <param name="mods">The mods to search.</param>
        /// <param name="warning">The mod warning to match.</param>
        /// <param name="level">The log level for the logged messages.</param>
        /// <param name="heading">A brief heading label for the group.</param>
        /// <param name="blurb">A detailed explanation of the warning, split into lines.</param>
        void LogModWarningGroup( IModMetadata[] mods, ModWarning warning, LogLevel level, string heading, params string[] blurb )
        {
            this.LogModWarningGroup( mods, mod => mod.HasUnsuppressedWarnings( warning ), level, heading, blurb );
        }

        /// <summary>Preload and mods and run cecil mods.</summary>
        /// <param name="mods">The mods to load.</param>
        /// <param name="jsonHelper">The JSON helper with which to read mods' JSON files.</param>
        /// <param name="contentCore">The content manager to use for mod content.</param>
        /// <param name="modDatabase">Handles access to SMAPI's internal mod metadata list.</param>
        private void PreLoadMods(IModMetadata[] mods, JsonHelper jsonHelper, ModDatabase modDatabase)
        {
            this.Monitor.Log("Preloading mods...", LogLevel.Trace);

            // load mods
            IDictionary<IModMetadata, Tuple<string, string>> skippedMods = new Dictionary<IModMetadata, Tuple<string, string>>();
            //using (AssemblyLoader modAssemblyLoader = new AssemblyLoader(Constants.Platform, this.Monitor, this.Settings.ParanoidWarnings, doingCecilMods: true))
            {
                // init
                HashSet<string> suppressUpdateChecks = new HashSet<string>(this.Settings.SuppressUpdateChecks, StringComparer.InvariantCultureIgnoreCase);
                void LogSkip(IModMetadata mod, string errorPhrase, string errorDetails)
                {
                    skippedMods[mod] = Tuple.Create(errorPhrase, errorDetails);
                    if (mod.Status != ModMetadataStatus.Failed)
                        mod.SetStatus(ModMetadataStatus.Failed, errorPhrase);
                }

                // load mods
                foreach (IModMetadata contentPack in mods)
                {
                    if (!this.TryPreLoadMod(contentPack, mods/*, modAssemblyLoader*/, jsonHelper, modDatabase, suppressUpdateChecks, out string errorPhrase, out string errorDetails))
                        LogSkip(contentPack, errorPhrase, errorDetails);
                }
            }
            IModMetadata[] loaded = this.ModRegistry.GetAll().ToArray();
            IModMetadata[] loadedContentPacks = loaded.Where(p => p.IsContentPack).ToArray();
            IModMetadata[] loadedMods = loaded.Where(p => !p.IsContentPack).ToArray();

            // log mod warnings
            this.LogModWarnings(loaded, skippedMods);

            // Cecil mods are initialized later by the game rewriter
        }
        

        /// <summary>Load a given mod.</summary>
        /// <param name="mod">The mod to load.</param>
        /// <param name="mods">The mods being loaded.</param>
        /// <param name="assemblyLoader">Preprocesses and loads mod assemblies</param>
        /// <param name="proxyFactory">Generates proxy classes to access mod APIs through an arbitrary interface.</param>
        /// <param name="jsonHelper">The JSON helper with which to read mods' JSON files.</param>
        /// <param name="contentCore">The content manager to use for mod content.</param>
        /// <param name="modDatabase">Handles access to SMAPI's internal mod metadata list.</param>
        /// <param name="suppressUpdateChecks">The mod IDs to ignore when validating update keys.</param>
        /// <param name="errorReasonPhrase">The user-facing reason phrase explaining why the mod couldn't be loaded (if applicable).</param>
        /// <param name="errorDetails">More detailed details about the error intended for developers (if any).</param>
        /// <returns>Returns whether the mod was successfully loaded.</returns>
        private bool TryPreLoadMod(IModMetadata mod, IModMetadata[] mods/*, AssemblyLoader assemblyLoader*/, JsonHelper jsonHelper, ModDatabase modDatabase, HashSet<string> suppressUpdateChecks, out string errorReasonPhrase, out string errorDetails)
        {
            errorDetails = null;

            // log entry
            {
                string relativePath = mod.GetRelativePathWithRoot();
                if (mod.IsContentPack)
                    this.Monitor.Log($"   {mod.DisplayName} (from {relativePath}) [content pack]...", LogLevel.Trace);
                else if (mod.Manifest?.EntryDll != null)
                    this.Monitor.Log($"   {mod.DisplayName} (from {relativePath}{Path.DirectorySeparatorChar}{mod.Manifest.EntryDll})...", LogLevel.Trace); // don't use Path.Combine here, since EntryDLL might not be valid
                else
                    this.Monitor.Log($"   {mod.DisplayName} (from {relativePath})...", LogLevel.Trace);
            }

            // add warning for missing update key
            if (mod.HasID() && !suppressUpdateChecks.Contains(mod.Manifest.UniqueID) && !mod.HasValidUpdateKeys())
                mod.SetWarning(ModWarning.NoUpdateKeys);

            // validate status
            if (mod.Status == ModMetadataStatus.Failed)
            {
                this.Monitor.Log($"      Failed: {mod.Error}", LogLevel.Trace);
                errorReasonPhrase = mod.Error;
                return false;
            }

            // validate dependencies
            // Although dependencies are validated before mods are loaded, a dependency may have failed to load.
            if (mod.Manifest.Dependencies?.Any() == true)
            {
                foreach (IManifestDependency dependency in mod.Manifest.Dependencies.Where(p => p.IsRequired))
                {
                    if (this.ModRegistry.Get(dependency.UniqueID) == null)
                    {
                        string dependencyName = mods
                            .FirstOrDefault(otherMod => otherMod.HasID(dependency.UniqueID))
                            ?.DisplayName ?? dependency.UniqueID;
                        errorReasonPhrase = $"it needs the '{dependencyName}' mod, which couldn't be loaded.";
                        return false;
                    }
                }
            }

            // load as content pack
            if (mod.IsContentPack)
            {
                this.ModRegistry.Add( mod );
                errorReasonPhrase = null;
                return true;
            }

            // load as mod
            else
            {
                IManifest manifest = mod.Manifest;

                // load mod
                string assemblyPath = manifest?.EntryDll != null
                    ? Path.Combine(mod.DirectoryPath, manifest.EntryDll)
                    : null;
                Assembly modAssembly = null;
                try
                {
                    if (mod.IsCecilMod)
                    {
                        modAssembly = Assembly.LoadFrom(assemblyPath);// assemblyLoader.Load(mod, assemblyPath, assumeCompatible: mod.DataRecord?.Status == ModStatus.AssumeCompatible);
                        this.ModRegistry.TrackAssemblies(mod, modAssembly);
                    }
                }
                catch (IncompatibleInstructionException) // details already in trace logs
                {
                    string[] updateUrls = new[] { modDatabase.GetModPageUrlFor(manifest.UniqueID), "https://smapi.io/mods" }.Where(p => p != null).ToArray();
                    errorReasonPhrase = $"it's no longer compatible. Please check for a new version at {string.Join(" or ", updateUrls)}";
                    return false;
                }
                catch (SAssemblyLoadFailedException ex)
                {
                    errorReasonPhrase = $"it DLL couldn't be loaded: {ex.Message}";
                    return false;
                }
                catch (Exception ex)
                {
                    errorReasonPhrase = "its DLL couldn't be loaded.";
                    errorDetails = $"Error: {ex.GetLogSummary()}";
                    return false;
                }

                // Non-cecil mods are loaded later
                if ( modAssembly == null )
                {
                    this.ModRegistry.Add( mod );
                    errorReasonPhrase = null;
                    return true;
                }

                try
                {
                    // get mod instance
                    if ( !this.TryLoadModEntry( modAssembly, out CecilMod modEntry, out errorReasonPhrase ) )
                        return false;

                    IMonitor monitor = this.GetModMonitor(mod.DisplayName);

                    modEntry.ModManifest = manifest;
                    modEntry.Monitor = monitor;

                    mod.SetMod( modEntry );
                    mod.SetWarning( ModWarning.EditsGame );
                    this.ModRegistry.Add(mod);

                    errorReasonPhrase = null;
                    return true;
                }
                catch (Exception ex)
                {
                    errorReasonPhrase = $"initialization failed:\n{ex.GetLogSummary()}";
                    return false;
                }
            }
        }
        
        /// <summary>Load and hook up the given mods.</summary>
        /// <param name="mods">The mods to load.</param>
        /// <param name="jsonHelper">The JSON helper with which to read mods' JSON files.</param>
        /// <param name="contentCore">The content manager to use for mod content.</param>
        /// <param name="modDatabase">Handles access to SMAPI's internal mod metadata list.</param>
        private void LoadMods( ModRegistry modRegistry, JsonHelper jsonHelper, ModInitStuff initStuff, ModDatabase modDatabase, Action<IModMetadata[]> reloadTranslations, IAssetIntercepter assetIntercepter )
        {
            this.Monitor.Log( "Loading mods...", LogLevel.Trace );
            IModMetadata[] mods = modRegistry.GetAll().ToArray();

            // load mods
            IDictionary<IModMetadata, Tuple<string, string>> skippedMods = new Dictionary<IModMetadata, Tuple<string, string>>();
            using ( AssemblyLoader modAssemblyLoader = new AssemblyLoader( Constants.Platform, this.Monitor, this.Settings.ParanoidWarnings ) )
            {
                // init
                HashSet<string> suppressUpdateChecks = new HashSet<string>(this.Settings.SuppressUpdateChecks, StringComparer.InvariantCultureIgnoreCase);
                InterfaceProxyFactory proxyFactory = new InterfaceProxyFactory();
                void LogSkip( IModMetadata mod, string errorPhrase, string errorDetails )
                {
                    skippedMods[ mod ] = Tuple.Create( errorPhrase, errorDetails );
                    modRegistry.Remove( mod );
                    if ( mod.Status != ModMetadataStatus.Failed )
                        mod.SetStatus( ModMetadataStatus.Failed, errorPhrase );
                }

                // load mods
                foreach ( IModMetadata contentPack in mods )
                {
                    if ( contentPack.IsCecilMod )
                        continue;

                    if ( !this.TryLoadMod( contentPack, mods, modAssemblyLoader, proxyFactory, jsonHelper, initStuff, modDatabase, suppressUpdateChecks, out string errorPhrase, out string errorDetails ) )
                        LogSkip( contentPack, errorPhrase, errorDetails );
                }
            }
            IModMetadata[] loaded = this.ModRegistry.GetAll().ToArray();
            IModMetadata[] loadedContentPacks = loaded.Where(p => p.IsContentPack).ToArray();
            IModMetadata[] loadedMods = loaded.Where(p => !p.IsContentPack).ToArray();

            // unlock content packs
            this.ModRegistry.AreAllModsLoaded = true;

            // log loaded mods
            this.Monitor.Log( $"Loaded {loadedMods.Length} mods" + ( loadedMods.Length > 0 ? ":" : "." ), LogLevel.Info );
            foreach ( IModMetadata metadata in loadedMods.OrderBy( p => p.DisplayName ) )
            {
                IManifest manifest = metadata.Manifest;
                this.Monitor.Log(
                    $"   {metadata.DisplayName} {manifest.Version}"
                    + ( !string.IsNullOrWhiteSpace( manifest.Author ) ? $" by {manifest.Author}" : "" )
                    + ( !string.IsNullOrWhiteSpace( manifest.Description ) ? $" | {manifest.Description}" : "" ),
                    LogLevel.Info
                );
            }
            this.Monitor.Newline();

            // log loaded content packs
            if ( loadedContentPacks.Any() )
            {
                string GetModDisplayName( string id ) => loadedMods.FirstOrDefault( p => p.HasID( id ) )?.DisplayName;

                this.Monitor.Log( $"Loaded {loadedContentPacks.Length} content packs:", LogLevel.Info );
                foreach ( IModMetadata metadata in loadedContentPacks.OrderBy( p => p.DisplayName ) )
                {
                    IManifest manifest = metadata.Manifest;
                    this.Monitor.Log(
                        $"   {metadata.DisplayName} {manifest.Version}"
                        + ( !string.IsNullOrWhiteSpace( manifest.Author ) ? $" by {manifest.Author}" : "" )
                        + $" | for {GetModDisplayName( metadata.Manifest.ContentPackFor.UniqueID )}"
                        + ( !string.IsNullOrWhiteSpace( manifest.Description ) ? $" | {manifest.Description}" : "" ),
                        LogLevel.Info
                    );
                }
                this.Monitor.Newline();
            }

            // log mod warnings
            this.LogModWarnings( loaded, skippedMods );

            // initialize translations
            reloadTranslations( loaded );

            // initialize loaded non-content-pack mods
            foreach ( IModMetadata metadata in loadedMods )
            {
                if ( metadata.IsCecilMod )
                    continue;
                var mod =  metadata.Mod as Mod;

                // add interceptors
                if ( mod.Helper.Content is ContentHelper helper )
                {
                    // ReSharper disable SuspiciousTypeConversion.Global
                    if ( mod is IAssetEditor editor )
                        initStuff.contentCore.Editors.Add( new ModLinked<IAssetEditor>( metadata, editor ) );
                    if ( mod is IAssetLoader loader )
                        initStuff.contentCore.Loaders.Add( new ModLinked<IAssetLoader>( metadata, loader ) );
                    // ReSharper restore SuspiciousTypeConversion.Global

                    helper.ObservableAssetEditors.CollectionChanged += ( sender, e ) => assetIntercepter.OnInterceptorsChanged( metadata, e.NewItems?.Cast<IAssetEditor>(), e.OldItems?.Cast<IAssetEditor>(), initStuff.contentCore.Editors );
                    helper.ObservableAssetLoaders.CollectionChanged += ( sender, e ) => assetIntercepter.OnInterceptorsChanged( metadata, e.NewItems?.Cast<IAssetLoader>(), e.OldItems?.Cast<IAssetLoader>(), initStuff.contentCore.Loaders );
                }

                // call entry method
                try
                {
                    mod.Entry( mod.Helper );
                }
                catch ( Exception ex )
                {
                    metadata.LogAsMod( $"Mod crashed on entry and might not work correctly. Technical details:\n{ex.GetLogSummary()}", LogLevel.Error );
                }

                // get mod API
                try
                {
                    object api = mod.GetApi();
                    if ( api != null && !api.GetType().IsPublic )
                    {
                        api = null;
                        this.Monitor.Log( $"{metadata.DisplayName} provides an API instance with a non-public type. This isn't currently supported, so the API won't be available to other mods.", LogLevel.Warn );
                    }

                    if ( api != null )
                        this.Monitor.Log( $"   Found mod-provided API ({api.GetType().FullName}).", LogLevel.Trace );
                    metadata.SetApi( api );
                }
                catch ( Exception ex )
                {
                    this.Monitor.Log( $"Failed loading mod-provided API for {metadata.DisplayName}. Integrations with other mods may not work. Error: {ex.GetLogSummary()}", LogLevel.Error );
                }
            }

            // invalidate cache entries when needed
            // (These listeners are registered after Entry to avoid repeatedly reloading assets as mods initialize.)
            foreach ( IModMetadata metadata in loadedMods )
            {
                if ( metadata.IsCecilMod )
                    continue;

                if ( ( metadata.Mod as Mod ).Helper.Content is ContentHelper helper )
                {
                    helper.ObservableAssetEditors.CollectionChanged += ( sender, e ) => assetIntercepter.OnAssetInterceptorsChanged(metadata, e.NewItems, e.OldItems);
                    helper.ObservableAssetLoaders.CollectionChanged += ( sender, e ) => assetIntercepter.OnAssetInterceptorsChanged(metadata, e.NewItems, e.OldItems);
                }
            }

            // unlock mod integrations
            this.ModRegistry.AreAllModsInitialized = true;
        }
        
        /// <summary>Load a given mod.</summary>
        /// <param name="mod">The mod to load.</param>
        /// <param name="mods">The mods being loaded.</param>
        /// <param name="assemblyLoader">Preprocesses and loads mod assemblies</param>
        /// <param name="proxyFactory">Generates proxy classes to access mod APIs through an arbitrary interface.</param>
        /// <param name="jsonHelper">The JSON helper with which to read mods' JSON files.</param>
        /// <param name="contentCore">The content manager to use for mod content.</param>
        /// <param name="modDatabase">Handles access to SMAPI's internal mod metadata list.</param>
        /// <param name="suppressUpdateChecks">The mod IDs to ignore when validating update keys.</param>
        /// <param name="errorReasonPhrase">The user-facing reason phrase explaining why the mod couldn't be loaded (if applicable).</param>
        /// <param name="errorDetails">More detailed details about the error intended for developers (if any).</param>
        /// <returns>Returns whether the mod was successfully loaded.</returns>
        private bool TryLoadMod( IModMetadata mod, IModMetadata[] mods, AssemblyLoader assemblyLoader, InterfaceProxyFactory proxyFactory, JsonHelper jsonHelper, ModInitStuff initStuff, ModDatabase modDatabase, HashSet<string> suppressUpdateChecks, out string errorReasonPhrase, out string errorDetails )
        {
            errorDetails = null;

            // log entry
            {
                string relativePath = mod.GetRelativePathWithRoot();
                if ( mod.IsContentPack )
                    this.Monitor.Log( $"   {mod.DisplayName} (from {relativePath}) [content pack]...", LogLevel.Trace );
                else if ( mod.Manifest?.EntryDll != null )
                    this.Monitor.Log( $"   {mod.DisplayName} (from {relativePath}{Path.DirectorySeparatorChar}{mod.Manifest.EntryDll})...", LogLevel.Trace ); // don't use Path.Combine here, since EntryDLL might not be valid
                else
                    this.Monitor.Log( $"   {mod.DisplayName} (from {relativePath})...", LogLevel.Trace );
            }

            // add warning for missing update key
            if ( mod.HasID() && !suppressUpdateChecks.Contains( mod.Manifest.UniqueID ) && !mod.HasValidUpdateKeys() )
                mod.SetWarning( ModWarning.NoUpdateKeys );

            // validate status
            if ( mod.Status == ModMetadataStatus.Failed )
            {
                this.Monitor.Log( $"      Failed: {mod.Error}", LogLevel.Trace );
                errorReasonPhrase = mod.Error;
                return false;
            }

            // validate dependencies
            // Although dependencies are validated before mods are loaded, a dependency may have failed to load.
            if ( mod.Manifest.Dependencies?.Any() == true )
            {
                foreach ( IManifestDependency dependency in mod.Manifest.Dependencies.Where( p => p.IsRequired ) )
                {
                    if ( this.ModRegistry.Get( dependency.UniqueID ) == null )
                    {
                        string dependencyName = mods
                            .FirstOrDefault(otherMod => otherMod.HasID(dependency.UniqueID))
                            ?.DisplayName ?? dependency.UniqueID;
                        errorReasonPhrase = $"it needs the '{dependencyName}' mod, which couldn't be loaded.";
                        return false;
                    }
                }
            }

            // load as content pack
            if ( mod.IsContentPack )
            {
                IManifest manifest = mod.Manifest;
                IMonitor monitor = this.GetModMonitor(mod.DisplayName);
                IContentHelper contentHelper = new ContentHelper(initStuff.contentCore, mod.DirectoryPath, manifest.UniqueID, mod.DisplayName, monitor);
                TranslationHelper translationHelper = new TranslationHelper(manifest.UniqueID, initStuff.contentCore.GetLocale(), initStuff.contentCore.Language);
                IContentPack contentPack = new ContentPack(mod.DirectoryPath, manifest, contentHelper, translationHelper, jsonHelper);
                mod.SetMod( contentPack, monitor, translationHelper );
                //this.ModRegistry.Add( mod );

                errorReasonPhrase = null;
                return true;
            }

            // load as mod
            else
            {
                IManifest manifest = mod.Manifest;

                // load mod
                string assemblyPath = manifest?.EntryDll != null
                    ? Path.Combine(mod.DirectoryPath, manifest.EntryDll)
                    : null;
                Assembly modAssembly;
                try
                {
                    modAssembly = assemblyLoader.Load( mod, assemblyPath, assumeCompatible: mod.DataRecord?.Status == ModStatus.AssumeCompatible );
                    this.ModRegistry.TrackAssemblies( mod, modAssembly );
                }
                catch ( IncompatibleInstructionException ) // details already in trace logs
                {
                    string[] updateUrls = new[] { modDatabase.GetModPageUrlFor(manifest.UniqueID), "https://smapi.io/mods" }.Where(p => p != null).ToArray();
                    errorReasonPhrase = $"it's no longer compatible. Please check for a new version at {string.Join( " or ", updateUrls )}";
                    return false;
                }
                catch ( SAssemblyLoadFailedException ex )
                {
                    errorReasonPhrase = $"its DLL couldn't be loaded: {ex.Message}";
                    return false;
                }
                catch ( Exception ex )
                {
                    errorReasonPhrase = "its DLL couldn't be loaded.";
                    errorDetails = $"Error: {ex.GetLogSummary()}";
                    return false;
                }

                // initialize mod
                try
                {
                    // get mod instance
                    if ( !this.TryLoadModEntry( modAssembly, out Mod modEntry, out errorReasonPhrase ) )
                        return false;

                    // get content packs
                    IContentPack[] GetContentPacks()
                    {
                        if ( !this.ModRegistry.AreAllModsLoaded )
                            throw new InvalidOperationException( "Can't access content packs before SMAPI finishes loading mods." );

                        return this.ModRegistry
                            .GetAll( assemblyMods: false )
                            .Where( p => p.IsContentPack && mod.HasID( p.Manifest.ContentPackFor.UniqueID ) )
                            .Select( p => p.ContentPack )
                            .ToArray();
                    }

                    // init mod helpers
                    IMonitor monitor = this.GetModMonitor(mod.DisplayName);
                    TranslationHelper translationHelper = new TranslationHelper(manifest.UniqueID, initStuff.contentCore.GetLocale(), initStuff.contentCore.Language);
                    IModHelper modHelper;
                    {
                        IContentPack CreateFakeContentPack( string packDirPath, IManifest packManifest )
                        {
                            IMonitor packMonitor = this.GetModMonitor(packManifest.Name);
                            IContentHelper packContentHelper = new ContentHelper(initStuff.contentCore, packDirPath, packManifest.UniqueID, packManifest.Name, packMonitor);
                            ITranslationHelper packTranslationHelper = new TranslationHelper(packManifest.UniqueID, initStuff.contentCore.GetLocale(), initStuff.contentCore.Language);
                            return new ContentPack( packDirPath, packManifest, packContentHelper, packTranslationHelper, this.Toolkit.JsonHelper );
                        }

                        IModEvents events = new ModEvents(mod, initStuff.eventManager);
                        ICommandHelper commandHelper = new CommandHelper(mod, initStuff.commandManager);
                        IContentHelper contentHelper = new ContentHelper(initStuff.contentCore, mod.DirectoryPath, manifest.UniqueID, mod.DisplayName, monitor);
                        IContentPackHelper contentPackHelper = new ContentPackHelper(manifest.UniqueID, new Lazy<IContentPack[]>(GetContentPacks), CreateFakeContentPack);
                        IDataHelper dataHelper = new DataHelper(manifest.UniqueID, mod.DirectoryPath, jsonHelper);
                        IReflectionHelper reflectionHelper = new ReflectionHelper(manifest.UniqueID, mod.DisplayName, initStuff.reflection);
                        IModRegistry modRegistryHelper = new ModRegistryHelper(manifest.UniqueID, this.ModRegistry, proxyFactory, monitor);
                        IMultiplayerHelper multiplayerHelper = new MultiplayerHelper(manifest.UniqueID, initStuff.multiplayer);

                        modHelper = new ModHelper( manifest.UniqueID, mod.DirectoryPath, initStuff.input, events, contentHelper, contentPackHelper, commandHelper, dataHelper, modRegistryHelper, reflectionHelper, multiplayerHelper, translationHelper );
                    }

                    // init mod
                    modEntry.ModManifest = manifest;
                    modEntry.Helper = modHelper;
                    modEntry.Monitor = monitor;

                    // track mod
                    mod.SetMod( modEntry, translationHelper );
                    //this.ModRegistry.Add( mod );
                    return true;
                }
                catch ( Exception ex )
                {
                    errorReasonPhrase = $"initialization failed:\n{ex.GetLogSummary()}";
                    return false;
                }
            }
        }

        /// <summary>Load a mod's entry class.</summary>
        /// <param name="modAssembly">The mod assembly.</param>
        /// <param name="mod">The loaded instance.</param>
        /// <param name="error">The error indicating why loading failed (if applicable).</param>
        /// <returns>Returns whether the mod entry class was successfully loaded.</returns>
        private bool TryLoadModEntry<TModType>( Assembly modAssembly, out TModType mod, out string error )
        {
            mod = default(TModType);

            // find type
            TypeInfo[] modEntries = modAssembly.DefinedTypes.Where(type => typeof(TModType).IsAssignableFrom(type) && !type.IsAbstract).Take(2).ToArray();
            if ( modEntries.Length == 0 )
            {
                error = $"its DLL has no '{nameof( TModType )}' subclass.";
                return false;
            }
            if ( modEntries.Length > 1 )
            {
                error = $"its DLL contains multiple '{nameof( TModType )}' subclasses.";
                return false;
            }

            // get implementation
            mod = ( TModType ) modAssembly.CreateInstance( modEntries[ 0 ].ToString() );
            if ( mod == null )
            {
                error = "its entry class couldn't be instantiated.";
                return false;
            }

            error = null;
            return true;
        }


    }
}
