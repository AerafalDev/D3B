using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace D3B.Studio.Assets;

/// <summary>A route: anchors, maps in order, and how it goes through them (D299).</summary>
public sealed record RouteAsset : Asset
{
    /// <summary>Gets the anchors, in the order the route goes through them.</summary>
    public ImmutableArray<RouteAnchor> Anchors { get; init; } = [];

    /// <summary>Gets how the route goes through its anchors.</summary>
    public RouteMode Mode { get; init; }

    /// <summary>Gets what the route does with a map no path reaches.</summary>
    public RouteUnreachable Unreachable { get; init; }

    /// <summary>Gets what the file holds but its anchors, mode and unreachable maps, kept as it is for the sessions that make it (7d3).</summary>
    public JsonObject Details { get; init; } = [];

    /// <inheritdoc/>
    public override AssetKind Kind => AssetKind.Route;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Uses()
    {
        return [];
    }
}
