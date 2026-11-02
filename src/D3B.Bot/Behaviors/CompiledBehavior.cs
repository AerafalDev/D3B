using System.Collections.Immutable;

namespace D3B.Bot.Behaviors;

/// <summary>A behavior once compiled with a profile's values, checked whole, ready to run: no reflection at each step (D297).</summary>
internal sealed class CompiledBehavior
{
    public required string Id { get; init; }

    public required ImmutableArray<CompiledState> Always { get; init; }

    public required ImmutableArray<CompiledState> States { get; init; }

    public required ImmutableArray<CompiledRule> Rules { get; init; }

    public required ImmutableArray<BehaviorVariable> Variables { get; init; }

    public bool HasUrgent { get; init; }

    /// <summary>Chooses the leaf from the state: the layer Always first, then the stack, the first whose conditions hold, down to a leaf.</summary>
    public CompiledState? Choose(Evaluation evaluation)
    {
        return CompiledState.ChooseIn(Always, evaluation) ?? CompiledState.ChooseIn(States, evaluation);
    }
}
