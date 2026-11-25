using System.Collections.Immutable;

namespace D3B.Engine.Tactics;

/// <summary>The first actions of a turn that tame a wild mount for its capture, and whether one may still be tamed (D533).</summary>
/// <remarks>Built only by its factories, so that a plan that acts is always pending.</remarks>
public sealed record TamingPlan
{
    /// <summary>Gets no taming, none left to come.</summary>
    public static TamingPlan None { get; } = new();

    /// <summary>Gets no taming this turn, one still to come: only the range, the line of sight or the AP left stand in the way.</summary>
    public static TamingPlan Later { get; } = new() { IsPending = true };

    /// <summary>Gets the walk, if one is needed, then the cast that tames a mount; none when no mount may be tamed this turn.</summary>
    public ImmutableArray<TacticAction> Actions { get; private init; } = [];

    /// <summary>Gets whether a mount may still be tamed, this turn or a later one: the taming spell is held, and only its range, its line of sight or the AP left stand in the way.</summary>
    public bool IsPending { get; private init; }

    /// <summary>Gets the taming of this turn.</summary>
    /// <param name="actions">The walk, if one is needed, then the cast.</param>
    /// <returns>The plan, pending.</returns>
    public static TamingPlan Now(ImmutableArray<TacticAction> actions)
    {
        return new TamingPlan { Actions = actions, IsPending = true };
    }
}
