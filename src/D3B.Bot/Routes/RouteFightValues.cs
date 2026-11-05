namespace D3B.Bot.Routes;

/// <summary>Which groups of monsters to fight on a map: a filter (D377).</summary>
public sealed class RouteFightValues
{
    /// <summary>Gets or sets the monsters a group must hold, by their ids; none, the default, every group.</summary>
    public IReadOnlyList<int> Monsters { get; set; } = [];

    /// <summary>Gets or sets the fewest monsters a group holds to be fought.</summary>
    public int LeastMonsters { get; set; } = 1;

    /// <summary>Gets or sets the most monsters a group holds to be fought.</summary>
    public int MostMonsters { get; set; } = 8;
}
