using System.Collections.Generic;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="Utility"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class UtilityFacade_1_6_16 : Utility, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public static bool CollectOrDrop(Item item, int direction)
    {
        return Game1.player.CollectOrDrop(item, direction);
    }

    public static bool CollectOrDrop(Item item)
    {
        return Game1.player.CollectOrDrop(item);
    }

    public static List<int> getDaysOfBooksellerThisSeason()
    {
        return Utility.getDaysOfBooksellerThisSeason(Game1.season, Game1.year);
    }


    /*********
    ** Private methods
    *********/
    private UtilityFacade_1_6_16()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
