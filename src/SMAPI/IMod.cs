namespace StardewModdingAPI
{
    /// <summary>The implementation for a Stardew Valley mod.</summary>
    public interface IMod : IBaseMod
    {
        /*********
        ** Accessors
        *********/
        /// <summary>Provides simplified APIs for writing mods.</summary>
        IModHelper Helper { get; }


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        void Entry(IModHelper helper);

        /// <summary>Get an API that other mods can access. This is always called after <see cref="Entry"/>.</summary>
        object GetApi();
    }
}
