using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace D3B.Studio.Assets;

/// <summary>A zone: a set of maps D3B goes through its own way, as harvesting does (D296, D299, D394).</summary>
/// <remarks>
/// It is the maps of its sub-areas and the maps named, less the maps left out. A map is named by its id, never by its coordinates,
/// which several maps share (D299).
/// </remarks>
public sealed record ZoneAsset : Asset
{
    /// <summary>Gets the sub-areas whose every map the zone holds, by id, ascending.</summary>
    public ImmutableArray<int> SubAreas { get; init; } = [];

    /// <summary>Gets the maps the zone holds besides, by id, ascending.</summary>
    public ImmutableArray<long> Maps { get; init; } = [];

    /// <summary>Gets the maps left out, those of a sub-area among them, by id, ascending.</summary>
    public ImmutableArray<long> Excluded { get; init; } = [];

    /// <summary>Gets what the file holds but its sub-areas, maps and left-out maps, kept as it is for the sessions that make it.</summary>
    public JsonObject Details { get; init; } = [];

    /// <inheritdoc/>
    public override AssetKind Kind => AssetKind.Zone;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Uses()
    {
        return [];
    }
}
