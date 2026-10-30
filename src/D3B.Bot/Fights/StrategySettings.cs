namespace D3B.Bot.Fights;

/// <summary>A fight's strategy: how the AI fights a character's fights, a profile of its own each character plays (D233, D234).</summary>
/// <remarks>Its defaults are "Standard", the AI as it fought before strategies, which a character without a strategy plays.</remarks>
public sealed class StrategySettings
{
    /// <summary>Gets or sets what the character does for its side.</summary>
    public StrategyRole Role { get; set; } = StrategyRole.Attacker;

    /// <summary>Gets or sets how much the AI fears the enemies' blows and holds the character's life.</summary>
    public StrategyStyle Style { get; set; } = StrategyStyle.Balanced;

    /// <summary>Gets or sets whether a fight judged too risky as it starts is played prudently (D198).</summary>
    public bool PrudentWhenRisky { get; set; } = true;

    /// <summary>Gets or sets whether the AI plays prudently once the character's life is under <see cref="PrudentBelow"/>.</summary>
    public bool PrudentUnder { get; set; }

    /// <summary>Gets or sets the share of its life under which the character plays prudently, in percent.</summary>
    public int PrudentBelow { get; set; } = 30;

    /// <summary>Gets or sets the spells the AI prefers, by id, of any breed: each character casts its own.</summary>
    public IReadOnlyList<int> Preferred { get; set; } = [];

    /// <summary>Gets or sets the spells the AI never casts, by id, of any breed.</summary>
    public IReadOnlyList<int> Excluded { get; set; } = [];

    /// <summary>Gets or sets whether the AI strikes with the character's weapon (D220).</summary>
    public bool Weapon { get; set; } = true;

    /// <summary>Gets or sets where the character stands as the fight starts.</summary>
    public StrategyPlacement Placement { get; set; } = StrategyPlacement.Automatic;

    /// <summary>Gets or sets whether the AI closes the fight to other players (D216).</summary>
    public bool Closed { get; set; } = true;

    /// <summary>Gets or sets whether the AI refuses spectators (D216).</summary>
    public bool NoSpectators { get; set; } = true;

    /// <summary>Gets or sets whether the AI plays the character's fights out of an activity, such as those the player starts, the bot doing nothing (D235).</summary>
    public bool OutOfActivity { get; set; }
}
