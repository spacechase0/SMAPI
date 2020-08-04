using Mono.Cecil;

namespace StardewModdingAPI
{
    /// <summary>The implementation for a Stardew Valley mod.</summary>
    public interface IRewriterMod : IBaseMod
    {
        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="gameAssembly">The game assembly.</param>
        void Entry(AssemblyDefinition gameAssembly);
    }
}
