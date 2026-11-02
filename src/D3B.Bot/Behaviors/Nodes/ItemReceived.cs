using D3B.Bot.Characters;

namespace D3B.Bot.Behaviors.Nodes;

/// <summary>More of an item in the inventory than there was, of some items or any.</summary>
internal sealed class ItemReceived : EventNode<AmountValues>
{
    public override bool Happened(BotChange change, AmountValues values)
    {
        if (!change.IsSameConnection || ReferenceEquals(change.Before.State.Inventory, change.After.State.Inventory))
            return false;

        var before = Counts(change.Before.State, values.Items);

        foreach (var (item, quantity) in Counts(change.After.State, values.Items))
        {
            if (quantity > before.GetValueOrDefault(item))
                return true;
        }

        return false;
    }

    private static Dictionary<int, long> Counts(CharacterState state, IReadOnlyList<int> items)
    {
        return state.Inventory.Values
            .Where(item => items.Count == 0 || items.Contains(item.ItemId))
            .GroupBy(item => item.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => (long)item.Quantity));
    }
}
