using System.Linq;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.GameData.Pets;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="PetData"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public abstract class PetDataFacade : PetData, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public PetBreed? GetBreedById(string breedId, bool allowNull = false)
    {
        return allowNull
            ? base.Breeds?.FirstOrDefault(p => p.Id == breedId)
            : base.GetPreferredBreed(breedId);
    }


    /*********
    ** Private methods
    *********/
    private PetDataFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
