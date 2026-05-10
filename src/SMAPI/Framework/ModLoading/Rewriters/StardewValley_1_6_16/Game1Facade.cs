using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.ContentManagement;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="Game1"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class Game1Facade_1_6_16 : Game1, IRewriteFacade
{
    /*********
    ** Accessors
    *********/
    public new static LocalizedContentManager content => (LocalizedContentManager)Game1.content;
    public new static LocalizedContentManager temporaryContent => (LocalizedContentManager)Game1.temporaryContent;
    public new LocalizedContentManager? xTileContent => (LocalizedContentManager?)base.xTileContent;


    /*********
    ** Private methods
    *********/
    private Game1Facade_1_6_16()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
