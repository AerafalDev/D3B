using D3B.Data.Items;

namespace D3B.Bot.Banks;

/// <summary>What the bank and the guild's chest take of what a character carries, and what an activity's deposit asks them of it.</summary>
/// <remarks>
/// Measured on the owner's four captures of 2026-11-25, harvesters unloading into the guild's chest: the chest refused each of a
/// quest's objects, ten items of the quest's category, with its text 356, and took every other stack; the bank then answered
/// nothing to the list of those objects alone, seven times up the ladder, and the harvest ended with its bags nearly empty.
/// </remarks>
public static class StoredItems
{
    /// <summary>Tells whether the bank and the guild's chest take an item.</summary>
    /// <param name="items">The client's items and their types.</param>
    /// <param name="itemId">The item's id in the client's tables.</param>
    /// <returns>Whether they do: all but a quest's objects; an item the tables lack too, which the server answers for.</returns>
    public static bool TakesInStorage(this ItemCatalog items, int itemId)
    {
        ArgumentNullException.ThrowIfNull(items);

        return !(items.TryGetItem(itemId, out var item) && items.TryGetType(item.TypeId, out var type) && type.CategoryId == (int)ItemCategory.Quest);
    }

    /// <summary>Tells whether an activity's deposit puts an item in the bank or the guild's chest.</summary>
    /// <param name="items">The client's items and their types.</param>
    /// <param name="itemId">The item's id in the client's tables.</param>
    /// <returns>
    /// Whether the storages take it and it weighs something: an unloading is for the pods, which a weightless item leaves as they
    /// are, the owner's choice, as BillBot's; an item the tables lack is deposited.
    /// </returns>
    public static bool IsDeposited(this ItemCatalog items, int itemId)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.TakesInStorage(itemId) && !(items.TryGetItem(itemId, out var item) && item.Weight is 0);
    }
}
