using System.Collections.Immutable;

namespace D3B.Studio.Behaviors;

/// <summary>A behavior that is a list of steps played in order, each a node of the catalog with its values (D386).</summary>
/// <param name="Steps">The steps, in order: a route followed, a harvest, a fight, a wait; each with the repeat it asks for (D420).</param>
/// <param name="Repeat">How the list is played again once played to its end: not at all, a number of times, for ever or until a condition holds.</param>
/// <remarks>
/// It is a shape of <see cref="Assets.BehaviorAsset"/>, not a model of its own: one state whose actions are chained by their way
/// on. The list editor reads and writes that shape; the canvas of 7e opens the same file.
/// </remarks>
public sealed record BehaviorSequence(ImmutableArray<NodeCall> Steps, StepRepeat Repeat)
{
    /// <summary>Gets a list with no step, played once.</summary>
    public static BehaviorSequence Empty { get; } = new([], StepRepeat.Once);

    /// <summary>Gets whether the list is played again once played to its end.</summary>
    public bool Repeats => Repeat.Kind != RepeatKind.Once;

    /// <summary>Tells whether two lists hold the same steps with the same values and repeats, and repeat alike.</summary>
    /// <param name="other">The other list.</param>
    /// <returns>Whether they are the same.</returns>
    public bool IsSame(BehaviorSequence other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Repeat.IsSame(other.Repeat) && Steps.Length == other.Steps.Length && Steps.Zip(other.Steps).All(pair => pair.First.IsSame(pair.Second));
    }
}
