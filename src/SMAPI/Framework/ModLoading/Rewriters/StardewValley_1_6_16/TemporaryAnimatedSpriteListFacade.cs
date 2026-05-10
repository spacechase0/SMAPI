using System.Collections.Generic;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="TemporaryAnimatedSpriteList"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public abstract class TemporaryAnimatedSpriteListFacade : TemporaryAnimatedSpriteList, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public new IEnumerator<TemporaryAnimatedSprite> GetEnumerator()
    {
        return base.GetEnumerator();
    }


    /*********
    ** Private methods
    *********/
    private TemporaryAnimatedSpriteListFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
