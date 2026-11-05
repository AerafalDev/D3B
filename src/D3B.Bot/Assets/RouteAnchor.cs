using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.Bot.Behaviors;

namespace D3B.Bot.Assets;

/// <summary>A map a route goes through, the truth of the route: the maps between two are found by the world's path (D299).</summary>
/// <remarks>
/// <para>It keeps a map's id, never coordinates, which several maps share.</para>
/// <para>
/// Two anchors are equal when what they hold is: the editor tells a route changed from the one saved by it, and a value
/// changed back leaves the route as it was.
/// </para>
/// </remarks>
public sealed record RouteAnchor
{
    /// <summary>Gets the anchor's map.</summary>
    public required long MapId { get; init; }

    /// <summary>Gets whether the anchor was a map of the world's path that the user pinned, which does not change what it is.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Gets what the route does on the map, in order: nodes of the catalog, each with its values (D377).</summary>
    public ImmutableArray<NodeCall> Actions { get; init; } = [];

    /// <summary>Gets what the anchor's file holds but its map, whether it is pinned and its actions, kept as it is for the sessions that make it.</summary>
    public JsonObject Details { get; init; } = [];

    /// <summary>Tells whether two actions hold the same node and the same values.</summary>
    /// <param name="first">An action.</param>
    /// <param name="second">Another.</param>
    /// <returns>Whether they are equal by what they hold.</returns>
    public static bool SameCall(NodeCall? first, NodeCall? second)
    {
        return CallComparer.Instance.Equals(first, second);
    }

    /// <inheritdoc/>
    public bool Equals(RouteAnchor? other)
    {
        return other is not null
            && MapId == other.MapId
            && IsPinned == other.IsPinned
            && Actions.AsSpan().SequenceEqual(other.Actions.AsSpan(), CallComparer.Instance)
            && JsonNode.DeepEquals(Details, other.Details);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(MapId, IsPinned, Actions.Length);
    }

    private sealed class CallComparer : IEqualityComparer<NodeCall>
    {
        public static CallComparer Instance { get; } = new();

        public bool Equals(NodeCall? x, NodeCall? y)
        {
            if (x is null || y is null)
                return x is null && y is null;

            if (x.Node != y.Node || x.Values.Count != y.Values.Count)
                return false;

            foreach (var (key, value) in x.Values)
            {
                if (!y.Values.TryGetValue(key, out var other) || !JsonNode.DeepEquals(value, other))
                    return false;
            }

            return true;
        }

        public int GetHashCode(NodeCall call)
        {
            return HashCode.Combine(call.Node, call.Values.Count);
        }
    }
}
