using D3B.Bot.Characters;
using D3B.Data.Items;

namespace D3B.Bot.Groups;

/// <summary>What a follower gives its master at the bank: each stack it carries and does not wear, never its kamas, its food only as its master's profile banks all (D459), as much as the master's pods still hold (D433, D442).</summary>
/// <remarks>
/// Pure functions of the two states, so that the master and its follower reckon the same gifts. The food that only heals
/// stays with the follower, which eats it before its master acts (D441), the lead's choice, unless the master's profile
/// banks all the character does not wear, the owner's ask (D459).
/// </remarks>
internal static class GroupGifts
{
    /// <summary>Gets the stacks, or parts of them, a follower gives its master now, in the order of their ids.</summary>
    /// <param name="follower">The follower's state.</param>
    /// <param name="master">The master's state, whose pods hold the gifts.</param>
    /// <param name="items">The pack's items, which say each one's weight.</param>
    /// <param name="food">Whether the food that only heals is given too.</param>
    /// <param name="left">The stacks an exchange left with the follower, given no more.</param>
    /// <returns>Each stack's id and the quantity given; none when the follower carries nothing to give, or the master's pods hold none of it.</returns>
    /// <remarks>An item the pack lacks weighs nothing here: the server answers what it takes.</remarks>
    public static IReadOnlyList<(int Uid, int Quantity)> Of(CharacterState follower, CharacterState master, ItemCatalog items, bool food, IReadOnlySet<int> left)
    {
        ArgumentNullException.ThrowIfNull(follower);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(left);

        var free = (long)master.MaxWeight - master.Weight;
        var gifts = new List<(int, int)>();

        foreach (var stack in follower.Inventory.Values.Where(stack => Given(stack, items, food) && !left.Contains(stack.Uid)).OrderBy(stack => stack.Uid))
        {
            var weight = items.TryGetItem(stack.ItemId, out var item) ? item.Weight : 0;
            var quantity = weight is 0 ? stack.Quantity : (int)Math.Min(stack.Quantity, Math.Max(0, free) / weight);

            if (quantity <= 0)
                break;

            gifts.Add((stack.Uid, quantity));
            free -= (long)weight * quantity;
        }

        return gifts;
    }

    /// <summary>Tells whether a follower carries anything to give, whatever the master's pods.</summary>
    /// <param name="follower">The follower's state.</param>
    /// <param name="items">The pack's items, which say what only heals.</param>
    /// <param name="food">Whether the food that only heals is given too.</param>
    /// <param name="left">The stacks an exchange left with the follower, given no more.</param>
    /// <returns>Whether it carries a stack it gives.</returns>
    public static bool Carries(CharacterState follower, ItemCatalog items, bool food, IReadOnlySet<int> left)
    {
        ArgumentNullException.ThrowIfNull(follower);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(left);

        return follower.Inventory.Values.Any(stack => Given(stack, items, food) && !left.Contains(stack.Uid));
    }

    private static bool Given(InventoryItem stack, ItemCatalog items, bool food)
    {
        return !stack.IsWorn && stack.Quantity > 0 && (food || !(items.TryGetItem(stack.ItemId, out var item) && item.Heals));
    }
}
