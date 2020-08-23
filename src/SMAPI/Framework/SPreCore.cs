using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using StardewModdingAPI.Framework.Logging;
using StardewModdingAPI.Framework.Models;
using StardewModdingAPI.Toolkit;
using StardewModdingAPI.Toolkit.Utilities;

namespace StardewModdingAPI.Framework
{
    /// <summary>
    /// Handles loading/setup that can be done before touching the game classes.
    /// </summary>
    public class SPreCore : IDisposable
    {
        /// <summary>The log file to which to write messages.</summary>
        internal readonly LogFileManager LogFile;

        /// <summary>Manages console output interception.</summary>
        internal readonly ConsoleInterceptionManager ConsoleManager = new ConsoleInterceptionManager();

        /// <summary>The core logger and monitor for SMAPI.</summary>
        internal readonly Monitor Monitor;

        /// <summary>The core logger and monitor on behalf of the game.</summary>
        internal readonly Monitor MonitorForGame;

        /// <summary>The SMAPI configuration settings.</summary>
        internal readonly SConfig Settings;

        /// <summary>Tracks the installed mods.</summary>
        internal readonly ModRegistry ModRegistry;

        internal readonly ModLoader ModLoader;

        /// <summary>The mod toolkit used for generic mod interactions.</summary>
        internal readonly ModToolkit Toolkit = new ModToolkit();

        /// <summary>Whether the program has been disposed.</summary>
        private bool IsDisposed;

        /// <summary>Regex patterns which match console non-error messages to suppress from the console and log.</summary>
        private readonly Regex[] SuppressConsolePatterns =
        {
            new Regex(@"^TextBox\.Selected is now '(?:True|False)'\.$", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            new Regex(@"^(?:FRUIT )?TREE: IsClient:(?:True|False) randomOutput: \d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            new Regex(@"^loadPreferences\(\); begin", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            new Regex(@"^savePreferences\(\); async=", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            new Regex(@"^DebugOutput:\s+(?:added CLOUD|added cricket|dismount tile|Ping|playerPos)", RegexOptions.Compiled | RegexOptions.CultureInvariant)
        };

        /// <summary>Regex patterns which match console messages to show a more friendly error for.</summary>
        private readonly ReplaceLogPattern[] ReplaceConsolePatterns =
        {
            // Steam not loaded
            new ReplaceLogPattern(
                search: new Regex(@"^System\.InvalidOperationException: Steamworks is not initialized\.[\s\S]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant),
                replacement:
#if SMAPI_FOR_WINDOWS
                    "Oops! Steam achievements won't work because Steam isn't loaded. You can launch the game through Steam to fix that (see 'Part 2: Configure Steam' in the install guide for more info: https://smapi.io/install).",
#else
                    "Oops! Steam achievements won't work because Steam isn't loaded. You can launch the game through Steam to fix that.",
#endif
                logLevel: LogLevel.Error
            ),

            // save file not found error
            new ReplaceLogPattern(
                search: new Regex(@"^System\.IO\.FileNotFoundException: [^\n]+\n[^:]+: '[^\n]+[/\\]Saves[/\\]([^'\r\n]+)[/\\]([^'\r\n]+)'[\s\S]+LoadGameMenu\.FindSaveGames[\s\S]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant),
                replacement: "The game can't find the '$2' file for your '$1' save. See https://stardewvalleywiki.com/Saves#Troubleshooting for help.",
                logLevel: LogLevel.Error
            )
        };

        public SPreCore(string[] args)
        {
            // get flags
            bool writeToConsole = !args.Contains("--no-terminal") && Environment.GetEnvironmentVariable("SMAPI_NO_TERMINAL") == null;

            // get mods path
            string modsPath;
            {
                string rawModsPath = null;

                // get from command line args
                int pathIndex = Array.LastIndexOf(args, "--mods-path") + 1;
                if (pathIndex >= 1 && args.Length >= pathIndex)
                    rawModsPath = args[pathIndex];

                // get from environment variables
                if (string.IsNullOrWhiteSpace(rawModsPath))
                    rawModsPath = Environment.GetEnvironmentVariable("SMAPI_MODS_PATH");

                // normalise
                modsPath = !string.IsNullOrWhiteSpace(rawModsPath)
                    ? Path.Combine(Constants.ExecutionPath, rawModsPath)
                    : Constants.DefaultModsPath;
            }

            // init basics
            this.Settings = JsonConvert.DeserializeObject<SConfig>(File.ReadAllText(Constants.ApiConfigPath));
            if (File.Exists(Constants.ApiUserConfigPath))
                JsonConvert.PopulateObject(File.ReadAllText(Constants.ApiUserConfigPath), this.Settings);

            // init paths
            Constants.ModsPath = modsPath;

            this.LogFile = InitLogFile();
            this.Monitor = new Monitor("SMAPI", this.ConsoleManager, this.LogFile, this.Settings.ConsoleColors, this.Settings.VerboseLogging)
            {
                WriteToConsole = writeToConsole,
                ShowTraceInConsole = this.Settings.DeveloperMode,
                ShowFullStampInConsole = this.Settings.DeveloperMode
            };
            this.MonitorForGame = this.GetSecondaryMonitor("game");

            // redirect direct console output
            if (this.MonitorForGame.WriteToConsole)
                this.ConsoleManager.OnMessageIntercepted += message => this.HandleConsoleMessage(this.MonitorForGame, message);

            // init logging
            this.Monitor.Log($"SMAPI {Constants.ApiVersion} with Stardew Valley ??? on {EnvironmentUtility.GetFriendlyPlatformName(Constants.Platform)}", LogLevel.Info);
            this.Monitor.Log($"Mods go here: {modsPath}", LogLevel.Info);
            if (modsPath != Constants.DefaultModsPath)
                this.Monitor.Log("(Using custom --mods-path argument.)", LogLevel.Trace);
            this.Monitor.Log($"Log started at {DateTime.UtcNow:s} UTC", LogLevel.Trace);

            // log custom settings
            {
                IDictionary<string, object> customSettings = this.Settings.GetCustomSettings();
                if (customSettings.Any())
                    this.Monitor.Log($"Loaded with custom settings: {string.Join(", ", customSettings.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value}"))}", LogLevel.Trace);
            }

            SPreCore.VerifyPath(modsPath);
            this.ModRegistry = new ModRegistry();

            this.ModLoader = new ModLoader(this.Monitor, this.ModRegistry, modsPath, this.Toolkit, this.Settings, this.GetSecondaryMonitor);
            this.ModLoader.PreLoadMods();
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
            this.ConsoleManager?.Dispose();
            this.LogFile?.Dispose();

            // end game (moved from Game1.OnExiting to let us clean up first)
            Process.GetCurrentProcess().Kill();
        }

        /// <summary>Create a directory path if it doesn't exist.</summary>
        /// <param name="path">The directory path.</param>
        internal static void VerifyPath(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                // note: this happens before a Monitor is initialized
                Console.WriteLine($"Couldn't create a path: {path}\n\n{ex.GetLogSummary()}");
            }
        }

        private static LogFileManager InitLogFile()
        {
            SPreCore.VerifyPath(Constants.LogDir);
            LogFileManager.PurgeNormalLogs();
            string logPath = LogFileManager.GetLogPath();
            LogFileManager logFile = new LogFileManager(logPath);
            return logFile;
        }

        /// <summary>Redirect messages logged directly to the console to the given monitor.</summary>
        /// <param name="gameMonitor">The monitor with which to log messages as the game.</param>
        /// <param name="message">The message to log.</param>
        private void HandleConsoleMessage(IMonitor gameMonitor, string message)
        {
            // detect exception
            LogLevel level = message.Contains("Exception") ? LogLevel.Error : LogLevel.Trace;

            // ignore suppressed message
            if (level != LogLevel.Error && this.SuppressConsolePatterns.Any(p => p.IsMatch(message)))
                return;

            // show friendly error if applicable
            foreach (ReplaceLogPattern entry in this.ReplaceConsolePatterns)
            {
                string newMessage = entry.Search.Replace(message, entry.Replacement);
                if (message != newMessage)
                {
                    gameMonitor.Log(newMessage, entry.LogLevel);
                    gameMonitor.Log(message, LogLevel.Trace);
                    return;
                }
            }

            // forward to monitor
            gameMonitor.Log(message, level);
        }

        /// <summary>Get a monitor instance derived from SMAPI's current settings.</summary>
        /// <param name="name">The name of the module which will log messages with this instance.</param>
        private Monitor GetSecondaryMonitor(string name)
        {
            return new Monitor(name, this.ConsoleManager, this.LogFile, this.Settings.ConsoleColors, this.Settings.VerboseLogging)
            {
                WriteToConsole = this.Monitor.WriteToConsole,
                ShowTraceInConsole = this.Settings.DeveloperMode,
                ShowFullStampInConsole = this.Settings.DeveloperMode
            };
        }

        /// <summary>A console log pattern to replace with a different message.</summary>
        private class ReplaceLogPattern
        {
            /*********
            ** Accessors
            *********/
            /// <summary>The regex pattern matching the portion of the message to replace.</summary>
            public Regex Search { get; }

            /// <summary>The replacement string.</summary>
            public string Replacement { get; }

            /// <summary>The log level for the new message.</summary>
            public LogLevel LogLevel { get; }


            /*********
            ** Public methods
            *********/
            /// <summary>Construct an instance.</summary>
            /// <param name="search">The regex pattern matching the portion of the message to replace.</param>
            /// <param name="replacement">The replacement string.</param>
            /// <param name="logLevel">The log level for the new message.</param>
            public ReplaceLogPattern(Regex search, string replacement, LogLevel logLevel)
            {
                this.Search = search;
                this.Replacement = replacement;
                this.LogLevel = logLevel;
            }
        }
    }
}
