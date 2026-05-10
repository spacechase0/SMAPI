using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.ContentManagement;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="NPC"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public abstract class NpcFacade_1_6_16 : NPC, IRewriteFacade
{
    /*********
    ** Accessors
    *********/
    public string? Birthday_Season =>
        base.Birthday_Season != null
            ? Utility.getSeasonNameFromNumber((int)base.Birthday_Season.Value)
            : null;


    /*********
    ** Public methods
    *********/
    public static NPC Constructor(AnimatedSprite sprite, Vector2 position, int facingDir, string name, LocalizedContentManager? content = null)
    {
        return new NPC(sprite, position, facingDir, name, content);
    }

    public bool TryLoadPortraits(string assetName, out string error, LocalizedContentManager? content = null)
    {
        return this.TryLoadPortraits(assetName, out error, (IContentManager?)content);
    }

    public bool TryLoadSprites(string assetName, out string error, LocalizedContentManager? content = null)
    {
        return this.TryLoadSprites(assetName, out error, (IContentManager?)content);
    }


    /*********
    ** Private methods
    *********/
    private NpcFacade_1_6_16()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
