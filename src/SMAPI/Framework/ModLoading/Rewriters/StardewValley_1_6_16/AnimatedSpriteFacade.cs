using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="AnimatedSprite"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class AnimatedSpriteFacade : AnimatedSprite, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public new void LoadTexture(string textureName, bool syncTextureName = true)
    {
        base.LoadTexture(textureName, syncTextureName);
    }


    /*********
    ** Private methods
    *********/
    private AnimatedSpriteFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
