using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="Forest"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class ForestFacade_1_6_16 : Forest, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public bool ShouldTravelingMerchantVisitToday()
    {
        return Forest.ShouldTravelingMerchantVisit(Game1.Date);
    }


    /*********
    ** Private methods
    *********/
    private ForestFacade_1_6_16()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
