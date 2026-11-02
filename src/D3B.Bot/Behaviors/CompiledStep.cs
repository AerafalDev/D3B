using D3B.Bot.Activities;

namespace D3B.Bot.Behaviors;

/// <summary>A node on the way of execution once compiled: an action, an activity, a branch or a variable set.</summary>
internal abstract class CompiledStep
{
    public required string Id { get; init; }

    /// <summary>Gets or sets the way on, linked once every node of the canvas is compiled.</summary>
    public CompiledStep? Next { get; set; }

    /// <summary>Gets or sets a branch's way when its condition does not hold.</summary>
    public CompiledStep? Otherwise { get; set; }

    public IEnumerable<CompiledStep> Ways()
    {
        if (Next is not null)
            yield return Next;

        if (Otherwise is not null)
            yield return Otherwise;
    }

    public sealed class Action : CompiledStep
    {
        public required IActionNode Node { get; init; }

        public required BoundValues Values { get; init; }

        public bool IsInstant => Node.IsInstant;
    }

    public sealed class Activity : CompiledStep
    {
        public required ActivityDefinition Definition { get; init; }

        public required BoundValues Settings { get; init; }
    }

    public sealed class Branch : CompiledStep
    {
        public required Func<Evaluation, bool> Condition { get; init; }
    }

    public sealed class SetVariable : CompiledStep
    {
        public required string Variable { get; init; }

        public required Func<Evaluation, object> Value { get; init; }
    }
}
