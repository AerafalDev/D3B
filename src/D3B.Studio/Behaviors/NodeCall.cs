using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace D3B.Studio.Behaviors;

/// <summary>A node of a kind a module declared, with its values, as a condition or an event uses it (D298).</summary>
/// <param name="Node">The node's kind, by its id.</param>
/// <param name="Values">
/// Its values by key, as JSON: a value chosen, or one bound to a public setting (<c>{"$param": "key"}</c>) or a variable
/// (<c>{"$var": "name"}</c>); a value left out keeps the kind's default.
/// </param>
public sealed record NodeCall(string Node, ImmutableSortedDictionary<string, JsonNode?> Values)
{
    /// <summary>Gets how many times the call is played when it is a step of a list; a route's action or a condition is played once (D420).</summary>
    public StepRepeat Repeat { get; init; } = StepRepeat.Once;

    /// <summary>Tells whether two calls are of the same node with the same values and the same repeat.</summary>
    /// <param name="other">The other call.</param>
    /// <returns>Whether they are the same.</returns>
    public bool IsSame(NodeCall other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Node == other.Node
            && Values.Count == other.Values.Count
            && Values.All(value => other.Values.TryGetValue(value.Key, out var same) && JsonNode.DeepEquals(value.Value, same))
            && Repeat.IsSame(other.Repeat);
    }
}
