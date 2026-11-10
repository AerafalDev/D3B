using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace D3B.Studio.Behaviors;

/// <summary>A node on a state's canvas: an action, a branch, a variable set or read, a condition (D297, D298).</summary>
/// <param name="Id">The node's id, unique in its behavior.</param>
/// <param name="Node">The node's kind, by its id.</param>
/// <param name="Values">Its values by key, as <see cref="NodeCall.Values"/> holds them; a value a wire brings is not kept here.</param>
public sealed record GraphNode(string Id, string Node, ImmutableSortedDictionary<string, JsonNode?> Values)
{
    /// <summary>Gets whether the node is disabled rather than deleted: the way goes on through it (D311).</summary>
    public bool IsDisabled { get; init; }

    /// <summary>Gets how many times the node is played when it is an action of a list (D420).</summary>
    public StepRepeat Repeat { get; init; } = StepRepeat.Once;

    /// <summary>Gets where the node stands on the canvas.</summary>
    public NodePosition Position { get; init; }
}
