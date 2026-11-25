using D3B.Data.Items;

namespace D3B.Bot.Transfers;

/// <summary>The transfer's settings: which way, what, how many, and what to do with a refusal (D560).</summary>
/// <remarks>An item moves when the transfer moves everything, or its category, its type or the item itself is chosen, and neither its type nor itself is excluded.</remarks>
public sealed class TransferSettings
{
    /// <summary>Gets or sets which way the objects go.</summary>
    public TransferDirection Direction { get; set; }

    /// <summary>Gets or sets whether every item the source holds moves, but those left out, in as many rounds as the pods need; else those the categories, types and items choose.</summary>
    public bool Everything { get; set; }

    /// <summary>Gets or sets the categories whose items move, such as every resource.</summary>
    public IReadOnlyList<ItemCategory> Categories { get; set; } = [];

    /// <summary>Gets or sets the types of items that move, by their ids, such as woods (D552).</summary>
    public IReadOnlyList<int> Types { get; set; } = [];

    /// <summary>Gets or sets the items that move, by their ids.</summary>
    public IReadOnlyList<int> Items { get; set; } = [];

    /// <summary>Gets or sets the types of items that never move, whatever chose them.</summary>
    public IReadOnlyList<int> ExcludedTypes { get; set; } = [];

    /// <summary>Gets or sets the items that never move, whatever chose them.</summary>
    public IReadOnlyList<int> ExcludedItems { get; set; } = [];

    /// <summary>Gets or sets how many of each item move.</summary>
    public TransferQuantity Quantity { get; set; }

    /// <summary>Gets or sets how many of each item move at most, or stay in the source, as <see cref="Quantity"/> says.</summary>
    public int Amount { get; set; } = 100;

    /// <summary>Gets or sets what the transfer does with an item the destination refuses.</summary>
    public TransferRefusal WhenRefused { get; set; }

    /// <summary>Gets or sets whether a transfer from the guild's chest first puts in the bank all the bags hold but what is worn, the character's own too, so that its pods are free (D564).</summary>
    public bool EmptyBagsFirst { get; set; }

    /// <summary>Gets or sets whether the transfer only opens the source and says what it would move, moving nothing.</summary>
    public bool DryRun { get; set; }

    /// <summary>Gets or sets whether the journeys to the bank and the chest may teleport to a zaap, as the general banking settings allow (D405, D436).</summary>
    public bool UseZaaps { get; set; } = true;
}
