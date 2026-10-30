namespace D3B.Engine.Tactics;

/// <summary>Where a fighter prefers to stand as the fight starts, among its team's placement cells (D234).</summary>
public enum TacticPlacement
{
    /// <summary>Where the judge weighs the enemies' threat against the damage it may deal best.</summary>
    Judged,

    /// <summary>As near the nearest enemy as its cells allow, the judge deciding a tie.</summary>
    NearEnemies,

    /// <summary>As far from the nearest enemy as its cells allow, the judge deciding a tie.</summary>
    FarFromEnemies,
}
