using D3B.Data.World;

namespace D3B.Studio.Assets;

/// <summary>Tells which maps a zone holds (D394).</summary>
public static class ZoneMaps
{
    /// <summary>Lists the maps a zone holds.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="atlas">The atlas, which says each map's sub-area.</param>
    /// <returns>The maps of the zone's sub-areas and the maps it names, less the maps left out.</returns>
    public static IReadOnlySet<long> Resolve(ZoneAsset zone, WorldAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(atlas);

        var maps = new HashSet<long>(zone.Maps);

        if (!zone.SubAreas.IsEmpty)
        {
            var subAreas = new HashSet<int>(zone.SubAreas);

            foreach (var position in atlas.Positions.Span)
            {
                if (subAreas.Contains(position.SubAreaId))
                    maps.Add(position.MapId);
            }
        }

        maps.ExceptWith(zone.Excluded);

        return maps;
    }

    /// <summary>Tells whether a zone holds a map.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="atlas">The atlas.</param>
    /// <param name="map">The map's id.</param>
    /// <returns>Whether the zone holds the map.</returns>
    public static bool Holds(ZoneAsset zone, WorldAtlas atlas, long map)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(atlas);

        if (zone.Excluded.Contains(map))
            return false;

        return zone.Maps.Contains(map) || (!zone.SubAreas.IsEmpty && atlas.TryGetPosition(map, out var position) && zone.SubAreas.Contains(position.SubAreaId));
    }
}
