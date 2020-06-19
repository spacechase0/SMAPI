using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
#if SMAPI_FOR_WINDOWS
using System.Windows.Forms;
#endif
using Newtonsoft.Json;
using StardewModdingAPI.Events;
using StardewModdingAPI.Framework.Commands;
using StardewModdingAPI.Framework.Events;
using StardewModdingAPI.Framework.Exceptions;
using StardewModdingAPI.Framework.Logging;
using StardewModdingAPI.Framework.Models;
using StardewModdingAPI.Framework.ModHelpers;
using StardewModdingAPI.Framework.ModLoading;
using StardewModdingAPI.Framework.Patching;
using StardewModdingAPI.Framework.PerformanceMonitoring;
using StardewModdingAPI.Framework.Reflection;
using StardewModdingAPI.Framework.Serialization;
using StardewModdingAPI.Patches;
using StardewModdingAPI.Toolkit;
using StardewModdingAPI.Toolkit.Framework.Clients.WebApi;
using StardewModdingAPI.Toolkit.Framework.ModData;
using StardewModdingAPI.Toolkit.Serialization;
using StardewModdingAPI.Toolkit.Utilities;
using StardewModdingAPI.Utilities;
using StardewValley;
using Object = StardewValley.Object;
using ThreadState = System.Threading.ThreadState;

namespace StardewModdingAPI.Framework
{
    /// <summary>The core class which initializes and manages SMAPI.</summary>
    internal class SCore : IDisposable, IAssetIntercepter
    {
        /*********
        ** Fields
        *********/
        /// <summary>The log file to which to write messages.</summary>
        internal readonly LogFileManager LogFile;

        /// <summary>Manages console output interception.</summary>
        internal readonly ConsoleInterceptionManager ConsoleManager;

        /// <summary>The core logger and monitor for SMAPI.</summary>
        internal readonly Monitor Monitor;

        /// <summary>The core logger and monitor on behalf of the game.</summary>
        internal readonly Monitor MonitorForGame;

        internal readonly ModLoader ModLoader;

        /// <summary>Tracks whether the game should exit immediately and any pending initialization should be cancelled.</summary>
        private readonly CancellationTokenSource CancellationToken = new CancellationTokenSource();

        /// <summary>Simplifies access to private game code.</summary>
        private readonly Reflector Reflection = new Reflector();

        /// <summary>Encapsulates access to SMAPI core translations.</summary>
        private readonly Translator Translator = new Translator();

        /// <summary>The SMAPI configuration settings.</summary>
        internal readonly SConfig Settings;

        /// <summary>The underlying game instance.</summary>
        private SGame GameInstance;

        /// <summary>The underlying content manager.</summary>
        private ContentCoordinator ContentCore => this.GameInstance.ContentCore;

        /// <summary>Tracks the installed mods.</summary>
        internal readonly ModRegistry ModRegistry;

        /// <summary>Manages SMAPI events for mods.</summary>
        private readonly EventManager EventManager;

        /// <summary>Whether the game is currently running.</summary>
        private bool IsGameRunning;

        /// <summary>Whether the program has been disposed.</summary>
        private bool IsDisposed;
        
        /// <summary>The mod toolkit used for generic mod interactions.</summary>
        private readonly ModToolkit Toolkit;


        /*********
        ** Accessors
        *********/
        /// <summary>Manages deprecation warnings.</summary>
        /// <remarks>This is initialized after the game starts. This is accessed directly because it's not part of the normal class model.</remarks>
        internal static DeprecationManager DeprecationManager { get; private set; }

        /// <summary>Manages performance counters.</summary>
        /// <remarks>This is initialized after the game starts. This is non-private for use by Console Commands.</remarks>
        internal static PerformanceMonitor PerformanceMonitor { get; private set; }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="modsPath">The path to search for mods.</param>
        /// <param name="writeToConsole">Whether to output log messages to the console.</param>
        public SCore(SPreCore preCore)
        {
            this.LogFile = preCore.LogFile;
            this.ConsoleManager = preCore.ConsoleManager;
            this.Monitor = preCore.Monitor;
            this.MonitorForGame = preCore.MonitorForGame;
            this.Settings = preCore.Settings;
            this.ModRegistry = preCore.ModRegistry;
            this.ModLoader = preCore.ModLoader;
            this.Toolkit = preCore.Toolkit;

            SCore.PerformanceMonitor = new PerformanceMonitor(this.Monitor);
            this.EventManager = new EventManager(this.ModRegistry, SCore.PerformanceMonitor);
            SCore.PerformanceMonitor.InitializePerformanceCounterCollections(this.EventManager);

            SCore.DeprecationManager = new DeprecationManager(this.Monitor, this.ModRegistry);

            SDate.Translations = this.Translator;

            // validate platform
#if SMAPI_FOR_WINDOWS
            if (Constants.Platform != Platform.Windows)
            {
                this.Monitor.Log("Oops! You're running Windows, but this version of SMAPI is for Linux or Mac. Please reinstall SMAPI to fix this.", LogLevel.Error);
                this.PressAnyKeyToExit();
                return;
            }
#else
            if (Constants.Platform == Platform.Windows)
            {
                this.Monitor.Log($"Oops! You're running {Constants.Platform}, but this version of SMAPI is for Windows. Please reinstall SMAPI to fix this.", LogLevel.Error);
                this.PressAnyKeyToExit();
                return;
            }
#endif
        }

        /// <summary>Launch SMAPI.</summary>
        [HandleProcessCorruptedStateExceptions, SecurityCritical] // let try..catch handle corrupted state exceptions
        public void RunInteractively()
        {
            // initialize SMAPI
            try
            {
                JsonConverter[] converters = {
                    new ColorConverter(),
                    new PointConverter(),
                    new Vector2Converter(),
                    new RectangleConverter()
                };
                foreach (JsonConverter converter in converters)
                    this.Toolkit.JsonHelper.JsonSettings.Converters.Add(converter);

                // add error handlers
#if SMAPI_FOR_WINDOWS
                Application.ThreadException += (sender, e) => this.Monitor.Log($"Critical thread exception: {e.Exception.GetLogSummary()}", LogLevel.Error);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
#endif
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => this.Monitor.Log($"Critical app domain exception: {e.ExceptionObject}", LogLevel.Error);

                // add more lenient assembly resolver
                AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => AssemblyLoader.ResolveAssembly(e.Name);

                // hook locale event
                LocalizedContentManager.OnLanguageChange += locale => this.OnLocaleChanged();

                // override game
                SGame.ConstructorHack = new SGameConstructorHack(this.Monitor, this.Reflection, this.Toolkit.JsonHelper, this.InitializeBeforeFirstAssetLoaded);
                this.GameInstance = new SGame(
                    monitor: this.Monitor,
                    monitorForGame: this.MonitorForGame,
                    reflection: this.Reflection,
                    translator: this.Translator,
                    eventManager: this.EventManager,
                    jsonHelper: this.Toolkit.JsonHelper,
                    modRegistry: this.ModRegistry,
                    deprecationManager: SCore.DeprecationManager,
                    performanceMonitor: SCore.PerformanceMonitor,
                    onGameInitialized: this.InitializeAfterGameStart,
                    onGameExiting: this.Dispose,
                    cancellationToken: this.CancellationToken,
                    logNetworkTraffic: this.Settings.LogNetworkTraffic
                );
                this.Translator.SetLocale(this.GameInstance.ContentCore.GetLocale(), this.GameInstance.ContentCore.Language);
                StardewValley.Program.gamePtr = this.GameInstance;

                // apply game patches
                new GamePatcher(this.Monitor).Apply(
                    new EventErrorPatch(this.MonitorForGame),
                    new DialogueErrorPatch(this.MonitorForGame, this.Reflection),
                    new ObjectErrorPatch(),
                    new LoadContextPatch(this.Reflection, this.GameInstance.OnLoadStageChanged),
                    new LoadErrorPatch(this.Monitor, this.GameInstance.OnSaveContentRemoved),
                    new ScheduleErrorPatch(this.MonitorForGame)
                );

                // add exit handler
                new Thread(() =>
                {
                    this.CancellationToken.Token.WaitHandle.WaitOne();
                    if (this.IsGameRunning)
                    {
                        try
                        {
                            File.WriteAllText(Constants.FatalCrashMarker, string.Empty);
                            File.Copy(this.LogFile.LogPath, Constants.FatalCrashLog, overwrite: true);
                        }
                        catch (Exception ex)
                        {
                            this.Monitor.Log($"SMAPI failed trying to track the crash details: {ex.GetLogSummary()}", LogLevel.Error);
                        }

                        this.GameInstance.Exit();
                    }
                }).Start();

                // set window titles
                this.GameInstance.Window.Title = $"Stardew Valley {GameConstants.GameVersion} - running SMAPI {Constants.ApiVersion}";
                Console.Title = $"SMAPI {Constants.ApiVersion} - running Stardew Valley {GameConstants.GameVersion}";
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"SMAPI failed to initialize: {ex.GetLogSummary()}", LogLevel.Error);
                this.PressAnyKeyToExit();
                return;
            }

            // check update marker
            if (File.Exists(Constants.UpdateMarker))
            {
                string rawUpdateFound = File.ReadAllText(Constants.UpdateMarker);
                if (SemanticVersion.TryParse(rawUpdateFound, out ISemanticVersion updateFound))
                {
                    if (Constants.ApiVersion.IsPrerelease() && updateFound.IsNewerThan(Constants.ApiVersion))
                    {
                        this.Monitor.Log("A new version of SMAPI was detected last time you played.", LogLevel.Error);
                        this.Monitor.Log($"You can update to {updateFound}: https://smapi.io.", LogLevel.Error);
                        this.Monitor.Log("Press any key to continue playing anyway. (This only appears when using a SMAPI beta.)", LogLevel.Info);
                        Console.ReadKey();
                    }
                }
                File.Delete(Constants.UpdateMarker);
            }

            // show details if game crashed during last session
            if (File.Exists(Constants.FatalCrashMarker))
            {
                this.Monitor.Log("The game crashed last time you played. If it happens repeatedly, see 'get help' on https://smapi.io.", LogLevel.Error);
                this.Monitor.Log("If you ask for help, make sure to share your SMAPI log: https://smapi.io/log.", LogLevel.Error);
                this.Monitor.Log("Press any key to delete the crash data and continue playing.", LogLevel.Info);
                Console.ReadKey();
                File.Delete(Constants.FatalCrashLog);
                File.Delete(Constants.FatalCrashMarker);
            }

            // add headers
            if (this.Settings.DeveloperMode)
                this.Monitor.Log($"You have SMAPI for developers, so the console will be much more verbose. You can disable developer mode by installing the non-developer version of SMAPI, or by editing {Constants.ApiConfigPath}.", LogLevel.Info);
            if (!this.Settings.CheckForUpdates)
                this.Monitor.Log($"You configured SMAPI to not check for updates. Running an old version of SMAPI is not recommended. You can enable update checks by reinstalling SMAPI or editing {Constants.ApiConfigPath}.", LogLevel.Warn);
            if (!this.Monitor.WriteToConsole)
                this.Monitor.Log("Writing to the terminal is disabled because the --no-terminal argument was received. This usually means launching the terminal failed.", LogLevel.Warn);
            this.Monitor.VerboseLog("Verbose logging enabled.");

            // update window titles
            this.GameInstance.Window.Title = $"Stardew Valley {GameConstants.GameVersion} - running SMAPI {Constants.ApiVersion}";
            Console.Title = $"SMAPI {Constants.ApiVersion} - running Stardew Valley {GameConstants.GameVersion}";

            // start game
            this.Monitor.Log("Starting game...", LogLevel.Debug);
            try
            {
                this.IsGameRunning = true;
                StardewValley.Program.releaseBuild = true; // game's debug logic interferes with SMAPI opening the game window
                this.GameInstance.Run();
            }
            catch (InvalidOperationException ex) when (ex.Source == "Microsoft.Xna.Framework.Xact" && ex.StackTrace.Contains("Microsoft.Xna.Framework.Audio.AudioEngine..ctor"))
            {
                this.Monitor.Log("The game couldn't load audio. Do you have speakers or headphones plugged in?", LogLevel.Error);
                this.Monitor.Log($"Technical details: {ex.GetLogSummary()}", LogLevel.Trace);
                this.PressAnyKeyToExit();
            }
            catch (FileNotFoundException ex) when (ex.Message == "Could not find file 'C:\\Program Files (x86)\\Steam\\SteamApps\\common\\Stardew Valley\\Content\\XACT\\FarmerSounds.xgs'.") // path in error is hardcoded regardless of install path
            {
                this.Monitor.Log("The game can't find its Content\\XACT\\FarmerSounds.xgs file. You can usually fix this by resetting your content files (see https://smapi.io/troubleshoot#reset-content ), or by uninstalling and reinstalling the game.", LogLevel.Error);
                this.Monitor.Log($"Technical details: {ex.GetLogSummary()}", LogLevel.Trace);
                this.PressAnyKeyToExit();
            }
            catch (Exception ex)
            {
                this.MonitorForGame.Log($"The game failed to launch: {ex.GetLogSummary()}", LogLevel.Error);
                this.PressAnyKeyToExit();
            }
            finally
            {
                this.Dispose();
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            // skip if already disposed
            if (this.IsDisposed)
                return;
            this.IsDisposed = true;
            this.Monitor.Log("Disposing...", LogLevel.Trace);

            // dispose mod data
            foreach (IModMetadata mod in this.ModRegistry.GetAll())
            {
                try
                {
                    (mod.Mod as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    mod.LogAsMod($"Mod failed during disposal: {ex.GetLogSummary()}.", LogLevel.Warn);
                }
            }

            // dispose core components
            this.IsGameRunning = false;
            this.ConsoleManager?.Dispose();
            this.ContentCore?.Dispose();
            this.CancellationToken?.Dispose();
            this.GameInstance?.Dispose();
            this.LogFile?.Dispose();

            // end game (moved from Game1.OnExiting to let us clean up first)
            Process.GetCurrentProcess().Kill();
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Initialize mods before the first game asset is loaded. At this point the core content managers are loaded (so mods can load their own assets), but the game is mostly uninitialized.</summary>
        private void InitializeBeforeFirstAssetLoaded()
        {
            if (this.CancellationToken.IsCancellationRequested)
            {
                this.Monitor.Log("SMAPI shutting down: aborting initialization.", LogLevel.Warn);
                return;
            }

            // init TMX support
            try
            {
                xTile.Format.FormatManager.Instance.RegisterMapFormat(new TMXTile.TMXFormat(Game1.tileSize / Game1.pixelZoom, Game1.tileSize / Game1.pixelZoom, Game1.pixelZoom, Game1.pixelZoom));
            }
            catch (Exception ex)
            {
                this.Monitor.Log("SMAPI couldn't load TMX support. Some mods may not work correctly.", LogLevel.Warn);
                this.Monitor.Log($"Technical details: {ex.GetLogSummary()}", LogLevel.Trace);
            }

            // load actual mods
            var mis = new ModInitStuff(); // for some reason new Class() { stuff = ... }; isn't working
            mis.contentCore = this.ContentCore;
            mis.eventManager = this.EventManager;
            mis.commandManager = this.GameInstance.CommandManager;
            mis.multiplayer = this.GameInstance.Multiplayer;
            mis.reflection = this.Reflection;
            mis.input = this.GameInstance.Input;
            this.ModLoader.LoadMods(mis, this.ReloadTranslations, this );

            // update window titles
            int modsLoaded = this.ModRegistry.GetAll().Count();
            this.GameInstance.Window.Title = $"Stardew Valley {GameConstants.GameVersion} - running SMAPI {Constants.ApiVersion} with {modsLoaded} mods";
            Console.Title = $"SMAPI {Constants.ApiVersion} - running Stardew Valley {GameConstants.GameVersion} with {modsLoaded} mods";
        }

        /// <summary>Initialize SMAPI and mods after the game starts.</summary>
        private void InitializeAfterGameStart()
        {
            // validate XNB integrity
            if (!this.ValidateContentIntegrity())
                this.Monitor.Log("SMAPI found problems in your game's content files which are likely to cause errors or crashes. Consider uninstalling XNB mods or reinstalling the game.", LogLevel.Error);

            // start SMAPI console
            new Thread(this.RunConsoleLoop).Start();
        }

        /// <summary>Handle the game changing locale.</summary>
        private void OnLocaleChanged()
        {
            this.ContentCore.OnLocaleChanged();

            // get locale
            string locale = this.ContentCore.GetLocale();
            LocalizedContentManager.LanguageCode languageCode = this.ContentCore.Language;

            // update core translations
            this.Translator.SetLocale(locale, languageCode);

            // update mod translation helpers
            foreach (IModMetadata mod in this.ModRegistry.GetAll())
                mod.Translations.SetLocale(locale, languageCode);
        }

        /// <summary>Run a loop handling console input.</summary>
        [SuppressMessage("ReSharper", "FunctionNeverReturns", Justification = "The thread is aborted when the game exits.")]
        private void RunConsoleLoop()
        {
            // prepare console
            this.Monitor.Log("Type 'help' for help, or 'help <cmd>' for a command's usage", LogLevel.Info);
            this.GameInstance.CommandManager
                .Add(new HelpCommand(this.GameInstance.CommandManager), this.Monitor)
#if HARMONY_2
                .Add(new HarmonySummaryCommand(), this.Monitor)
#endif
                .Add(new ReloadI18nCommand(this.ReloadTranslations), this.Monitor);

            // start handling command line input
            Thread inputThread = new Thread(() =>
            {
                while (true)
                {
                    // get input
                    string input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    // handle command
                    this.Monitor.LogUserInput(input);
                    this.GameInstance.CommandQueue.Enqueue(input);
                }
            });
            inputThread.Start();

            // keep console thread alive while the game is running
            while (this.IsGameRunning && !this.CancellationToken.IsCancellationRequested)
                Thread.Sleep(1000 / 10);
            if (inputThread.ThreadState == ThreadState.Running)
                inputThread.Abort();
        }

        /// <summary>Look for common issues with the game's XNB content, and log warnings if anything looks broken or outdated.</summary>
        /// <returns>Returns whether all integrity checks passed.</returns>
        private bool ValidateContentIntegrity()
        {
            this.Monitor.Log("Detecting common issues...", LogLevel.Trace);
            bool issuesFound = false;

            // object format (commonly broken by outdated files)
            {
                // detect issues
                bool hasObjectIssues = false;
                void LogIssue(int id, string issue) => this.Monitor.Log($@"Detected issue: item #{id} in Content\Data\ObjectInformation.xnb is invalid ({issue}).", LogLevel.Trace);
                foreach (KeyValuePair<int, string> entry in Game1.objectInformation)
                {
                    // must not be empty
                    if (string.IsNullOrWhiteSpace(entry.Value))
                    {
                        LogIssue(entry.Key, "entry is empty");
                        hasObjectIssues = true;
                        continue;
                    }

                    // require core fields
                    string[] fields = entry.Value.Split('/');
                    if (fields.Length < Object.objectInfoDescriptionIndex + 1)
                    {
                        LogIssue(entry.Key, "too few fields for an object");
                        hasObjectIssues = true;
                        continue;
                    }

                    // check min length for specific types
                    switch (fields[Object.objectInfoTypeIndex].Split(new[] { ' ' }, 2)[0])
                    {
                        case "Cooking":
                            if (fields.Length < Object.objectInfoBuffDurationIndex + 1)
                            {
                                LogIssue(entry.Key, "too few fields for a cooking item");
                                hasObjectIssues = true;
                            }
                            break;
                    }
                }

                // log error
                if (hasObjectIssues)
                {
                    issuesFound = true;
                    this.Monitor.Log(@"Your Content\Data\ObjectInformation.xnb file seems to be broken or outdated.", LogLevel.Warn);
                }
            }

            return !issuesFound;
        }

        /// <summary>Handle a mod adding or removing asset interceptors.</summary>
        /// <typeparam name="T">The asset interceptor type (one of <see cref="IAssetEditor"/> or <see cref="IAssetLoader"/>).</typeparam>
        /// <param name="mod">The mod metadata.</param>
        /// <param name="added">The interceptors that were added.</param>
        /// <param name="removed">The interceptors that were removed.</param>
        /// <param name="list">The list to update.</param>
        private void OnInterceptorsChanged<T>(IModMetadata mod, IEnumerable<T> added, IEnumerable<T> removed, IList<ModLinked<T>> list)
        {
            foreach (T interceptor in added ?? new T[0])
                list.Add(new ModLinked<T>(mod, interceptor));

            foreach (T interceptor in removed ?? new T[0])
            {
                foreach (ModLinked<T> entry in list.Where(p => p.Mod == mod && object.ReferenceEquals(p.Data, interceptor)).ToArray())
                    list.Remove(entry);
            }
        }

        /// <summary>Reload translations for all mods.</summary>
        private void ReloadTranslations()
        {
            this.ReloadTranslations(this.ModRegistry.GetAll(contentPacks: false));
        }

        /// <summary>Reload translations for the given mods.</summary>
        /// <param name="mods">The mods for which to reload translations.</param>
        private void ReloadTranslations(IEnumerable<IModMetadata> mods)
        {
            // core SMAPI translations
            {
                var translations = this.ReadTranslationFiles(Path.Combine(Constants.InternalFilesPath, "i18n"), out IList<string> errors);
                if (errors.Any() || !translations.Any())
                {
                    this.Monitor.Log("SMAPI couldn't load some core translations. You may need to reinstall SMAPI.", LogLevel.Warn);
                    foreach (string error in errors)
                        this.Monitor.Log($"  - {error}", LogLevel.Warn);
                }
                this.Translator.SetTranslations(translations);
            }

            // mod translations
            foreach (IModMetadata metadata in mods)
            {
                if ( metadata.IsCecilMod )
                    continue;

                var translations = this.ReadTranslationFiles(Path.Combine(metadata.DirectoryPath, "i18n"), out IList<string> errors);
                if (errors.Any())
                {
                    metadata.LogAsMod("Mod couldn't load some translation files:", LogLevel.Warn);
                    foreach (string error in errors)
                        metadata.LogAsMod($"  - {error}", LogLevel.Warn);
                }
                metadata.Translations.SetTranslations(translations);
            }
        }

        /// <summary>Read translations from a directory containing JSON translation files.</summary>
        /// <param name="folderPath">The folder path to search.</param>
        /// <param name="errors">The errors indicating why translation files couldn't be parsed, indexed by translation filename.</param>
        private IDictionary<string, IDictionary<string, string>> ReadTranslationFiles(string folderPath, out IList<string> errors)
        {
            JsonHelper jsonHelper = this.Toolkit.JsonHelper;

            // read translation files
            var translations = new Dictionary<string, IDictionary<string, string>>();
            errors = new List<string>();
            DirectoryInfo translationsDir = new DirectoryInfo(folderPath);
            if (translationsDir.Exists)
            {
                foreach (FileInfo file in translationsDir.EnumerateFiles("*.json"))
                {
                    string locale = Path.GetFileNameWithoutExtension(file.Name.ToLower().Trim());
                    try
                    {
                        if (!jsonHelper.ReadJsonFileIfExists(file.FullName, out IDictionary<string, string> data))
                        {
                            errors.Add($"{file.Name} file couldn't be read"); // should never happen, since we're iterating files that exist
                            continue;
                        }

                        translations[locale] = data;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{file.Name} file couldn't be parsed: {ex.GetLogSummary()}");
                        continue;
                    }
                }
            }

            // validate translations
            foreach (string locale in translations.Keys.ToArray())
            {
                // handle duplicates
                HashSet<string> keys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                HashSet<string> duplicateKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string key in translations[locale].Keys.ToArray())
                {
                    if (!keys.Add(key))
                    {
                        duplicateKeys.Add(key);
                        translations[locale].Remove(key);
                    }
                }
                if (duplicateKeys.Any())
                    errors.Add($"{locale}.json has duplicate translation keys: [{string.Join(", ", duplicateKeys)}]. Keys are case-insensitive.");
            }

            return translations;
        }

        /// <summary>Show a 'press any key to exit' message, and exit when they press a key.</summary>
        private void PressAnyKeyToExit()
        {
            this.Monitor.Log("Game has ended. Press any key to exit.", LogLevel.Info);
            this.PressAnyKeyToExit(showMessage: false);
        }

        /// <summary>Show a 'press any key to exit' message, and exit when they press a key.</summary>
        /// <param name="showMessage">Whether to print a 'press any key to exit' message to the console.</param>
        private void PressAnyKeyToExit(bool showMessage)
        {
            if (showMessage)
                Console.WriteLine("Game has ended. Press any key to exit.");
            Thread.Sleep(100);
            Console.ReadKey();
            Environment.Exit(0);
        }

        public void OnInterceptorsChanged( IModMetadata mod, IEnumerable<IAssetEditor> added, IEnumerable<IAssetEditor> removed, IList<ModLinked<IAssetEditor>> list )
        {
            this.OnInterceptorsChanged<IAssetEditor>( mod, added, removed, list );
        }

        public void OnInterceptorsChanged( IModMetadata mod, IEnumerable<IAssetLoader> added, IEnumerable<IAssetLoader> removed, IList<ModLinked<IAssetLoader>> list )
        {
            this.OnInterceptorsChanged<IAssetLoader>( mod, added, removed, list );
        }

        public void OnAssetInterceptorsChanged( IModMetadata mod, IList newItems, IList oldItems )
        {
            this.GameInstance.OnAssetInterceptorsChanged( mod, newItems, oldItems );
        }
    }
}
