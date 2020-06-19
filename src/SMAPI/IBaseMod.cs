namespace StardewModdingAPI
{
    /// <summary>The implementation for a Stardew Valley mod.</summary>
    public interface IBaseMod
    {
        /*********
        ** Accessors
        *********/
        /// <summary>Writes messages to the console and log file.</summary>
        IMonitor Monitor { get; }

        /// <summary>The mod's manifest.</summary>
        IManifest ModManifest { get; }
    }
}
