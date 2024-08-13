using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConsoleWrapperLib;
using StardewModdingAPI.Toolkit.Utilities;

namespace StardewModdingAPI.Internal.ConsoleWriting
{
    /// <summary>Writes color-coded text to a ConsoleWrapper object.</summary>
    internal class ConsoleWrapperConsoleWriter : ColorfulConsoleWriter
    {
        /// <summary>The console wrapper object to use.</summary>
        private ConsoleWrapper ConsoleWrapper;

        /// <summary>Construct an instance.</summary>
        /// <param name="platform">The target platform.</param>
        /// <param name="consoleWrapper">The console wrapper object.</param>
        /// <param name="colorConfig">The colors to use for text written to the SMAPI console.</param>
        public ConsoleWrapperConsoleWriter(Platform platform, ConsoleWrapper consoleWrapper, ColorSchemeConfig colorConfig)
            : base(platform, colorConfig)
        {
            this.ConsoleWrapper = consoleWrapper;
        }

        /// <inheritdoc/>
        protected override void WriteLineImpl(string message, ConsoleColor? foregroundColor, ConsoleColor? backgroundColor)
        {
            this.ConsoleWrapper.WriteLine(message, foregroundColor ?? this.ConsoleWrapper.DefaultForeground, backgroundColor ?? this.ConsoleWrapper.DefaultBackground);
        }
    }
}
