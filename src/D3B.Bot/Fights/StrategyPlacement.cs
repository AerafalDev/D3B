namespace D3B.Bot.Fights;

/// <summary>Where a character's strategy places it as the fight starts (D234).</summary>
public enum StrategyPlacement
{
    /// <summary>Where the AI judges best.</summary>
    Automatic,

    /// <summary>As near the enemies as its cells allow.</summary>
    Near,

    /// <summary>As far from the enemies as its cells allow.</summary>
    Far,
}
