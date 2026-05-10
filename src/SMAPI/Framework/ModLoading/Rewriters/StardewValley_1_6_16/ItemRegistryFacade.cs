using StardewValley;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="ItemRegistry"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class ItemRegistryFacade : IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public static Item? Create(string itemId, int amount = 1, int quality = 0, bool allowNull = false)
    {
        return allowNull
            ? ItemRegistry.CreateOrNull(itemId, amount, quality)
            : ItemRegistry.Create(itemId, amount, quality);
    }

    public static TItem? Create<TItem>(string itemId, int amount = 1, int quality = 0, bool allowNull = false)
        where TItem : Item
    {
        return CreateGeneric<TItem>(itemId, amount, quality, allowNull);
    }

    // referenced from InstructionMetadata
    public static TItem? CreateGeneric<TItem>(string itemId, int amount = 1, int quality = 0, bool allowNull = false)
        where TItem : Item
    {
        return allowNull
            ? ItemRegistry.CreateOrNull<TItem>(itemId, amount, quality)
            : ItemRegistry.Create<TItem>(itemId, amount, quality);
    }
}
