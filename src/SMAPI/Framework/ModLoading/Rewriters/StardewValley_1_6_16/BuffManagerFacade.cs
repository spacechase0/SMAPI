using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Buffs;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="BuffManager"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class BuffManagerFacade : BuffManager, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public void Apply(Buff buff)
    {
        base.ApplyLocal(buff);
    }


    /*********
    ** Private methods
    *********/
    private BuffManagerFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
