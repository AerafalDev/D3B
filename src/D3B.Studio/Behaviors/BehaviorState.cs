using System.Collections.Immutable;

namespace D3B.Studio.Behaviors;

/// <summary>A state of a behavior: its conditions of entry, and either the states it holds or its actions (D297).</summary>
/// <param name="Id">The state's id, unique in its behavior.</param>
/// <param name="Name">The state's name, as its author wrote it.</param>
public sealed record BehaviorState(string Id, string Name)
{
    /// <summary>Gets what must hold for the state to be chosen; none, always.</summary>
    public Condition? When { get; init; }

    /// <summary>Gets whether the state, once its conditions hold, cuts the step running, as an interruption does (D297).</summary>
    public bool IsUrgent { get; init; }

    /// <summary>Gets how many times the state's actions are played from the first: once and then wait for another state, a number of times, for ever or until a condition holds (D421).</summary>
    public StepRepeat Repeat { get; init; } = StepRepeat.Once;

    /// <summary>Gets the states it holds, by priority, the first that holds chosen.</summary>
    public ImmutableArray<BehaviorState> States { get; init; } = [];

    /// <summary>Gets its actions, a leaf's.</summary>
    public ActionGraph Actions { get; init; } = ActionGraph.Empty;
}
