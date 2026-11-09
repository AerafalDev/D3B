namespace D3B.Studio.Behaviors.Nodes;

/// <summary>The kamas the character carries, or how many of some items its inventory holds, at least an amount.</summary>
internal sealed class AmountAtLeast : ConditionNode<AmountValues>
{
    public override bool Holds(BehaviorView view, AmountValues values)
    {
        if (values.Items.Count == 0)
            return view.State.Kamas >= values.Amount;

        var held = view.State.Inventory.Values.Where(item => values.Items.Contains(item.ItemId)).Sum(item => (long)item.Quantity);

        return held >= values.Amount;
    }
}
