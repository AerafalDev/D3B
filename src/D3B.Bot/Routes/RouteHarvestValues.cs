namespace D3B.Bot.Routes;

/// <summary>What to harvest on a map: a list of resources (D377).</summary>
public sealed class RouteHarvestValues
{
    /// <summary>Gets or sets the resources, by their items' ids.</summary>
    public IReadOnlyList<int> Resources { get; set; } = [];
}
