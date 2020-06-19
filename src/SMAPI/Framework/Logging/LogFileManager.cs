using System;
using System.IO;
using StardewModdingAPI.Toolkit.Utilities;

namespace StardewModdingAPI.Framework.Logging
{
    /// <summary>Manages reading and writing to log file.</summary>
    internal class LogFileManager : IDisposable
    {
        /*********
        ** Fields
        *********/
        /// <summary>The underlying stream writer.</summary>
        private readonly StreamWriter Stream;


        /*********
        ** Accessors
        *********/
        /// <summary>The full path to the log file being written.</summary>
        public string LogPath { get; }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="path">The log file to write.</param>
        public LogFileManager(string path)
        {
            this.LogPath = path;

            // create log directory if needed
            string logDir = System.IO.Path.GetDirectoryName(path);
            if (logDir == null)
                throw new ArgumentException($"The log path '{path}' is not valid.");
            Directory.CreateDirectory(logDir);

            // open log file stream
            this.Stream = new StreamWriter(path, append: false) { AutoFlush = true };
        }

        /// <summary>Write a message to the log.</summary>
        /// <param name="message">The message to log.</param>
        public void WriteLine(string message)
        {
            // always use Windows-style line endings for convenience
            // (Linux/Mac editors are fine with them, Windows editors often require them)
            this.Stream.Write(message + "\r\n");
        }

        /// <summary>Release all resources.</summary>
        public void Dispose()
        {
            this.Stream.Dispose();
        }

        /// <summary>Delete normal (non-crash) log files created by SMAPI.</summary>
        public static void PurgeNormalLogs()
        {
            DirectoryInfo logsDir = new DirectoryInfo(Constants.LogDir);
            if ( !logsDir.Exists )
                return;

            foreach ( FileInfo logFile in logsDir.EnumerateFiles() )
            {
                // skip non-SMAPI file
                if ( !logFile.Name.StartsWith( Constants.LogNamePrefix, StringComparison.InvariantCultureIgnoreCase ) )
                    continue;

                // skip crash log
                if ( logFile.FullName == Constants.FatalCrashLog )
                    continue;

                // delete file
                try
                {
                    FileUtilities.ForceDelete( logFile );
                }
                catch ( IOException )
                {
                    // ignore file if it's in use
                }
            }
        }
        
        /// <summary>Get the absolute path to the next available log file.</summary>
        public static string GetLogPath()
        {
            // default path
            {
                FileInfo defaultFile = new FileInfo(Path.Combine(Constants.LogDir, $"{Constants.LogFilename}.{Constants.LogExtension}"));
                if ( !defaultFile.Exists )
                    return defaultFile.FullName;
            }

            // get first disambiguated path
            for ( int i = 2; i < int.MaxValue; i++ )
            {
                FileInfo file = new FileInfo(Path.Combine(Constants.LogDir, $"{Constants.LogFilename}.player-{i}.{Constants.LogExtension}"));
                if ( !file.Exists )
                    return file.FullName;
            }

            // should never happen
            throw new InvalidOperationException( "Could not find an available log path." );
        }
    }
}
