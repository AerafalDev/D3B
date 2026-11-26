using D3B.Bot.Characters;
using D3B.Data.Dungeons;

namespace D3B.Bot.Dungeons;

/// <summary>The keys a group's master spares a follower who lacks them, before a dungeon's entry (D569).</summary>
/// <remarks>
/// The owner's choice: before each entry the master keeps what its own entry needs, none when its keyring opens the dungeon,
/// and gives each follower what it lacks for that entry. Each run reckons again, so that the group's keys are shared run by
/// run: a master in a group never enters without its followers (D528), so keys kept for its later runs would open nothing.
/// Pure functions of the states, so that the master's turn and its door reckon the same keys.
/// </remarks>
internal static class DungeonKeys
{
    /// <summary>Gets what a character lacks of the objects a dungeon requires.</summary>
    /// <param name="state">The character.</param>
    /// <param name="dungeons">The dungeons.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Each item lacking and how many; none when it carries them all, or a keyring that opens the dungeon.</returns>
    public static IReadOnlyList<(int ItemId, int Quantity)> Lacking(CharacterState state, DungeonCatalog dungeons, Dungeon dungeon)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(dungeons);

        if (OpensByKeyring(state, dungeon))
            return [];

        return [.. Required(dungeons, dungeon).Select(pair => (ItemId: pair.Key, Quantity: pair.Value - DungeonActivity.Carried(state, pair.Key))).Where(lack => lack.Quantity > 0)];
    }

    /// <summary>Gets what a master carries of a dungeon's objects beyond what its own entry needs.</summary>
    /// <param name="master">The master.</param>
    /// <param name="dungeons">The dungeons.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Each item and how many it may give; all it carries when its keyring opens the dungeon.</returns>
    public static Dictionary<int, int> Spare(CharacterState master, DungeonCatalog dungeons, Dungeon dungeon)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(dungeons);

        var keyring = OpensByKeyring(master, dungeon);
        var spare = new Dictionary<int, int>();

        foreach (var (itemId, quantity) in Required(dungeons, dungeon))
        {
            var left = DungeonActivity.Carried(master, itemId) - (keyring ? 0 : quantity);

            if (left > 0)
                spare[itemId] = left;
        }

        return spare;
    }

    /// <summary>Takes from what a master spares what a follower lacks, when it covers all of it.</summary>
    /// <param name="spare">What the master spares, lessened only when it covers the whole lack.</param>
    /// <param name="lacking">What the follower lacks.</param>
    /// <returns>Whether the master's keys cover it.</returns>
    public static bool TryCover(Dictionary<int, int> spare, IReadOnlyList<(int ItemId, int Quantity)> lacking)
    {
        ArgumentNullException.ThrowIfNull(spare);
        ArgumentNullException.ThrowIfNull(lacking);

        if (lacking.Any(lack => spare.GetValueOrDefault(lack.ItemId) < lack.Quantity))
            return false;

        foreach (var (itemId, quantity) in lacking)
            spare[itemId] -= quantity;

        return true;
    }

    /// <summary>Gets the master's stacks, or parts of them, that hold the items given, in the order of their ids.</summary>
    /// <param name="master">The master.</param>
    /// <param name="items">Each item given and how many.</param>
    /// <returns>Each stack's id and the quantity moved from it.</returns>
    public static IReadOnlyList<(int Uid, int Quantity)> StacksOf(CharacterState master, IReadOnlyList<(int ItemId, int Quantity)> items)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(items);

        var stacks = new List<(int, int)>();

        foreach (var (itemId, quantity) in items)
        {
            var wanted = quantity;

            foreach (var stack in master.Inventory.Values.Where(stack => stack.ItemId == itemId && !stack.IsWorn && stack.Quantity > 0).OrderBy(stack => stack.Uid))
            {
                if (wanted <= 0)
                    break;

                var moved = Math.Min(wanted, stack.Quantity);

                stacks.Add((stack.Uid, moved));
                wanted -= moved;
            }
        }

        return stacks;
    }

    private static bool OpensByKeyring(CharacterState state, Dungeon dungeon)
    {
        return dungeon.IsOnKeyring && DungeonActivity.Carried(state, DungeonCatalog.KeyringItemId) > 0;
    }

    // A dungeon's objects by item, as the table may name one item twice.
    private static Dictionary<int, int> Required(DungeonCatalog dungeons, Dungeon dungeon)
    {
        var required = new Dictionary<int, int>();

        foreach (var item in dungeons.ObjectsOf(dungeon))
            required[item.ItemId] = required.GetValueOrDefault(item.ItemId) + item.Quantity;

        return required;
    }
}
