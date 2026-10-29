using System.Collections.Immutable;
using D3B.Bot.Activities;

namespace D3B.Bot.Harvesting;

/// <summary>A tour of the zone: the maps gone through, and those the way to failed, until every map is gone through once (D128).</summary>
/// <remarks>
/// A tour leaves out the maps the host's bots saw holding none of its resources (<see cref="HarvestSightings"/>), and goes
/// through every other once, those never seen included; then the next begins. The activity goes to the nearest map still
/// to go through, so a tour sweeps the zone, and the maps gone through longest ago come first in the next.
/// </remarks>
internal sealed class HarvestTour
{
    private readonly HashSet<long> _visited = [];
    private readonly HashSet<long> _unreachable = [];

    /// <summary>Gets the tour's number, from 1.</summary>
    public int Round { get; private set; } = 1;

    /// <summary>Gets whether anything was harvested in this tour.</summary>
    public bool HasHarvested { get; private set; }

    /// <summary>Gets how many maps this tour went through.</summary>
    public int Visited => _visited.Count;

    /// <summary>Gets how many maps a tour goes through: those not seen holding none of the zone's resources.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="sightings">What the host's bots saw.</param>
    /// <returns>How many.</returns>
    public static int Total(HarvestZone zone, HarvestSightings sightings)
    {
        return zone.Maps.Keys.Count(map => !sightings.IsEmptyOf(map, zone.Wanted));
    }

    /// <summary>Gets the maps still to go through in this tour, those of the world map first, then the insides.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="sightings">What the host's bots saw.</param>
    /// <returns>The zone's maps neither gone through, nor failed to reach, nor seen holding none of its resources: its insides only once none of the others is left.</returns>
    /// <remarks>
    /// Measured on the pack of 3.7.12.12: 114 of the 186 sub-areas listing resources hold insides too, 1,493 maps against
    /// 6,569, mostly houses; but the mines are insides of such sub-areas, so a tour keeps them, last. Doors cost a change
    /// each way, and a first tour through a forest's houses harvested nothing for minutes, measured.
    /// </remarks>
    public HashSet<long> Remaining(HarvestZone zone, HarvestSightings sightings)
    {
        HashSet<long> remaining = [.. zone.Maps.Keys.Where(map => !_visited.Contains(map) && !_unreachable.Contains(map) && !sightings.IsEmptyOf(map, zone.Wanted))];

        return remaining.Any(map => !zone.Insides.Contains(map)) ? [.. remaining.Where(map => !zone.Insides.Contains(map))] : remaining;
    }

    /// <summary>Marks a map gone through.</summary>
    /// <param name="mapId">The map.</param>
    public void Visit(long mapId)
    {
        _visited.Add(mapId);
    }

    /// <summary>Marks a map the way to failed: left out of the rest of the tour.</summary>
    /// <param name="mapId">The map.</param>
    public void Unreach(long mapId)
    {
        _unreachable.Add(mapId);
    }

    /// <summary>Marks a harvest in this tour.</summary>
    public void Harvested()
    {
        HasHarvested = true;
    }

    /// <summary>Begins the next tour, once this one went through every map.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="sightings">What the host's bots saw.</param>
    /// <returns>Whether a map of the zone may still hold a resource: a tour has something to go through.</returns>
    public bool Next(HarvestZone zone, HarvestSightings sightings)
    {
        if (Total(zone, sightings) is 0)
            return false;

        _visited.Clear();
        _unreachable.Clear();
        HasHarvested = false;
        Round++;

        return true;
    }

    /// <summary>Gets each map of the zone, with where the tour stands in it.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="sightings">What the host's bots saw.</param>
    /// <returns>The maps, for the app's map.</returns>
    public ImmutableArray<ZoneMap> Maps(HarvestZone zone, HarvestSightings sightings)
    {
        return [.. zone.Maps.Select(map => new ZoneMap(map.Key, StateOf(map.Key, zone, sightings), map.Value))];
    }

    private ZoneMapState StateOf(long mapId, HarvestZone zone, HarvestSightings sightings)
    {
        if (_visited.Contains(mapId))
            return ZoneMapState.Visited;

        if (sightings.IsEmptyOf(mapId, zone.Wanted))
            return ZoneMapState.Empty;

        return _unreachable.Contains(mapId) ? ZoneMapState.Unreachable : ZoneMapState.ToVisit;
    }
}
