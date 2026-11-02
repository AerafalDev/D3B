namespace D3B.Bot.Behaviors;

/// <summary>A reaction of the layer Always: when an event happens and its conditions hold, actions played at once (D297, D298).</summary>
/// <param name="Id">The rule's id, unique in its behavior.</param>
/// <param name="Name">The rule's name, as its author wrote it.</param>
/// <param name="On">The event.</param>
/// <remarks>Its actions are those played at once, such as setting a variable, a note, a pause or a stop: a rule never waits.</remarks>
public sealed record BehaviorRule(string Id, string Name, NodeCall On)
{
    /// <summary>Gets what must hold too; none, always.</summary>
    public Condition? When { get; init; }

    /// <summary>Gets the actions played.</summary>
    public ActionGraph Actions { get; init; } = ActionGraph.Empty;
}
