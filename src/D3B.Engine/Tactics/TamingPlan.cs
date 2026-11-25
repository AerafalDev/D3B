using System.Collections.Immutable;

namespace D3B.Engine.Tactics;

/// <summary>The first actions of a turn that tame a wild mount for its capture, and whether one may still be tamed (D533).</summary>
public sealed record TamingPlan
{
    /// <summary>Gets no taming, none left to come.</summary>
    public static TamingPlan None { get; } = new();

    /// <summary>Gets the walk, if one is needed, then the cast that tames a mount; none when no mount may be tamed this turn.</summary>
    public ImmutableArray<TacticAction> Actions { get; init; } = [];

    /// <summary>Gets whether a mount may still be tamed, this turn or a later one: the taming spell is held, and only its range, its line of sight or the AP left stand in the way.</summary>
    public bool IsPending { get; init; }
}
