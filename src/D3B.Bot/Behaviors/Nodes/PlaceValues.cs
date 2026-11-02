namespace D3B.Bot.Behaviors.Nodes;

/// <summary>Places of the world: areas or sub-areas.</summary>
public sealed class PlaceValues
{
    /// <summary>Gets or sets the areas.</summary>
    public IReadOnlyList<int> Areas { get; set; } = [];

    /// <summary>Gets or sets the sub-areas.</summary>
    public IReadOnlyList<int> SubAreas { get; set; } = [];
}
