namespace D3B.Bot.Behaviors.Nodes;

/// <summary>How a fight ended.</summary>
public sealed class FightValues
{
    /// <summary>Gets or sets how, lost by default.</summary>
    public FightResult Result { get; set; } = FightResult.Lost;
}
