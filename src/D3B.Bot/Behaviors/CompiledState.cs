using System.Collections.Immutable;

namespace D3B.Bot.Behaviors;

/// <summary>A state once compiled: its conditions of entry, and the states it holds or its first action (D297).</summary>
internal sealed class CompiledState
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public CompiledCondition? When { get; init; }

    public bool IsUrgent { get; init; }

    public bool Repeats { get; init; }

    public ImmutableArray<CompiledState> States { get; init; } = [];

    public CompiledStep? Entry { get; init; }

    public static CompiledState? ChooseIn(ImmutableArray<CompiledState> states, Evaluation evaluation)
    {
        foreach (var state in states)
        {
            if (state.When is { } when && !when.Holds(evaluation))
                continue;

            if (state.States.IsEmpty)
                return state;

            // A state whose states all fail is passed, as StateTree does: the next of its own rank may hold.
            if (ChooseIn(state.States, evaluation) is { } leaf)
                return leaf;
        }

        return null;
    }
}
