namespace D3B.Bot.Fights;

/// <summary>How much a character's strategy fears the enemies' blows and holds its life against the damage it deals: the judge's weights (D234).</summary>
public enum StrategyStyle
{
    /// <summary>The weights of a fight judged too risky (D198).</summary>
    Prudent,

    /// <summary>The weights measured on the captures (D212).</summary>
    Balanced,

    /// <summary>Fears the enemies' blows and holds its life less, and weighs next turn's damage more.</summary>
    Offensive,
}
