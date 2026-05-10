using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Objects;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="Farmer"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class FarmerFacade_1_6_16 : Farmer, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public bool addItemToInventoryBool(Item? item, bool makeActiveObject = false)
    {
        if (item is null || !this.IsLocalPlayer)
            return false;

        AddToInventoryResult result = base.TryAddToInventory(item);

        if (makeActiveObject && result.AnyAdded && item is not SpecialItem && result.Remainder != null && item.Stack <= 1)
        {
            int newItemPosition = this.getIndexOfInventoryItem(item);
            if (newItemPosition > -1)
                (this.Items[this.CurrentToolIndex], this.Items[newItemPosition]) = (this.Items[newItemPosition], this.Items[this.CurrentToolIndex]); // swap into active position
        }

        return result.AnyAdded;
    }

    public void applyBuff(Buff buff)
    {
        base.applyLocalBuff(buff);
    }

    public void dropItem(Item? i)
    {
        if (i is null || !i.canBeDropped())
            return;


        this.DropItemAtFeet(i, direction: this.FacingDirection);
    }


    /*********
    ** Private methods
    *********/
    private FarmerFacade_1_6_16()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
