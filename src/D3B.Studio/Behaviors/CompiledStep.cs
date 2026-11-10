using D3B.Bot.Activities;
using D3B.Bot.Localization;

namespace D3B.Studio.Behaviors;

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

        /// <summary>Gets the node's sentence, which the trace says.</summary>
        public required LocalizedText Sentence { get; init; }

        public required BoundValues Values { get; init; }

        public bool IsInstant => Node.IsInstant;

        /// <summary>Gets how many passes the action is played (D421).</summary>
        public StepRepeat Repeat { get; init; } = StepRepeat.Once;

        /// <summary>Gets the condition of an <see cref="RepeatKind.Until"/> repeat, compiled.</summary>
        public CompiledCondition? Until { get; init; }
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
