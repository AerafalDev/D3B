using System.Collections.Immutable;

namespace D3B.Bot.Behaviors;

/// <summary>A state of a behavior: its conditions of entry, and either the states it holds or its actions (D297).</summary>
/// <param name="Id">The state's id, unique in its behavior.</param>
/// <param name="Name">The state's name, as its author wrote it.</param>
public sealed record BehaviorState(string Id, string Name)
{
    /// <summary>Gets what must hold for the state to be chosen; none, always.</summary>
    public Condition? When { get; init; }

    /// <summary>Gets whether the state, once its conditions hold, cuts the step running, as an interruption does (D297).</summary>
    public bool IsUrgent { get; init; }

    /// <summary>Gets whether the state's actions start again once played to their end, rather than wait for another state.</summary>
    public bool Repeats { get; init; }

    /// <summary>Gets the states it holds, by priority, the first that holds chosen.</summary>
    public ImmutableArray<BehaviorState> States { get; init; } = [];

    /// <summary>Gets its actions, a leaf's.</summary>
    public ActionGraph Actions { get; init; } = ActionGraph.Empty;
}
