namespace D3B.Bot.Behaviors;

/// <summary>A behavior compiled with a profile's values: ready to run, or what is wrong with it (D297, D302).</summary>
public sealed class BehaviorCompilation
{
    /// <summary>Gets what is wrong, none when the behavior runs.</summary>
    public IReadOnlyList<BehaviorProblem> Problems { get; }

    /// <summary>Gets whether the behavior runs.</summary>
    public bool Holds => Behavior is not null;

    internal CompiledBehavior? Behavior { get; }

    internal BehaviorCompilation(CompiledBehavior? behavior, IReadOnlyList<BehaviorProblem> problems)
    {
        Behavior = behavior;
        Problems = problems;
    }
}
