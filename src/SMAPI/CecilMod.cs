using System;
using Mono.Cecil;

namespace StardewModdingAPI
{
    /// <summary>The base class for a mod.</summary>
    public abstract class CecilMod : ICecilMod, IDisposable
    {
        /*********
        ** Accessors
        *********/
        /// <summary>Writes messages to the console and log file.</summary>
        public IMonitor Monitor { get; internal set; }

        /// <summary>The mod's manifest.</summary>
        public IManifest ModManifest { get; internal set; }


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="gameAssembly">The game assembly.</param>
        public abstract void Entry(AssemblyDefinition gameAssembly);

        /// <summary>Release or reset unmanaged resources.</summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Release or reset unmanaged resources when the game exits. There's no guarantee this will be called on every exit.</summary>
        /// <param name="disposing">Whether the instance is being disposed explicitly rather than finalized. If this is false, the instance shouldn't dispose other objects since they may already be finalized.</param>
        protected virtual void Dispose(bool disposing) { }

        /// <summary>Destruct the instance.</summary>
        ~CecilMod()
        {
            this.Dispose(false);
        }
    }
}
