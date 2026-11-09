using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace D3B.Studio.Behaviors;

/// <summary>A behavior that is a list of steps played in order, each a node of the catalog with its values (D386).</summary>
/// <param name="Steps">The steps, in order: a route followed, a harvest, a fight, a wait.</param>
/// <param name="Repeats">Whether the list starts again once played to its end, rather than ends the run.</param>
/// <remarks>
/// It is a shape of <see cref="Assets.BehaviorAsset"/>, not a model of its own: one state whose actions are chained by their way
/// on. The list editor reads and writes that shape; the canvas of 7e opens the same file.
/// </remarks>
public sealed record BehaviorSequence(ImmutableArray<NodeCall> Steps, bool Repeats)
{
    /// <summary>Gets a list with no step, played once.</summary>
    public static BehaviorSequence Empty { get; } = new([], false);

    /// <summary>Tells whether two lists hold the same steps with the same values and repeat alike.</summary>
    /// <param name="other">The other list.</param>
    /// <returns>Whether they are the same.</returns>
    public bool IsSame(BehaviorSequence other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Repeats == other.Repeats && Steps.Length == other.Steps.Length && Steps.Zip(other.Steps).All(pair => pair.First.Node == pair.Second.Node && SameValues(pair.First, pair.Second));
    }

    private static bool SameValues(NodeCall first, NodeCall second)
    {
        return first.Values.Count == second.Values.Count && first.Values.All(value => second.Values.TryGetValue(value.Key, out var other) && JsonNode.DeepEquals(value.Value, other));
    }
}
