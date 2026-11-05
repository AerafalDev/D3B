using System.Text.Json.Nodes;

namespace D3B.Bot.Assets;

/// <summary>A map a route goes through, the truth of the route: the maps between two anchors are found by the world's path (D299).</summary>
/// <remarks>It keeps a map's id, never coordinates, which several maps share.</remarks>
public sealed record RouteAnchor
{
    /// <summary>Gets the anchor's map.</summary>
    public required long MapId { get; init; }

    /// <summary>Gets whether the anchor was a map of the world's path that the user pinned, which does not change what it is.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Gets what the anchor's file holds but its map and whether it is pinned, kept as it is for the sessions that make it (7d3).</summary>
    public JsonObject Details { get; init; } = [];
}
