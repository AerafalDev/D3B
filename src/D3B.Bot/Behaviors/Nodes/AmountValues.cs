namespace D3B.Bot.Behaviors.Nodes;

/// <summary>An amount, such as of kamas, and the items it counts, if any.</summary>
public sealed class AmountValues
{
    /// <summary>Gets or sets the items counted, for an amount of items.</summary>
    public IReadOnlyList<int> Items { get; set; } = [];

    /// <summary>Gets or sets the amount, 1 by default.</summary>
    public int Amount { get; set; } = 1;
}
