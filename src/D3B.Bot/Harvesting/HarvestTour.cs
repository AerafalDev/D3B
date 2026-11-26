using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;

namespace D3B.Bot.Harvesting;

/// <summary>A tour of the zone: the maps gone through, and those the way to failed, until every map is gone through once (D128).</summary>
/// <remarks>
/// <para>
/// A tour leaves out the maps the host's bots saw holding none of its resources (<see cref="HarvestSightings"/>), and those
/// never seen whose elements' graphics are all known to offer none (<see cref="HarvestZone.Foresee"/>, D138). It goes
/// through every other once, then the next begins.
/// </para>
/// <para>
/// It goes first to the probable maps, those seen holding a resource wanted or drawing a graphic that offers one, then to
/// the others of the world map, then to the insides. The activity goes to the nearest map still to go through, so a tour
/// sweeps the probable maps first, and the maps gone through longest ago come first in the next.
/// </para>
/// <para>
/// A map whose wanted elements all still grow back, by what the host learned of their resources, waits until the first
/// should have (<see cref="Regrowths"/>, D145); a resource whose growing back is not known yet never holds a map back.
/// </para>
/// </remarks>
internal sealed class HarvestTour
{
    /// <summary>How many journeys in a row may arrive with nothing done since the one before, before the tour is said to go round (D488).</summary>
    public const int MostRounds = 3;

    private readonly HashSet<long> _visited = [];
    private readonly HashSet<long> _crossed = [];
    private readonly HashSet<long> _unreachable = [];
    private readonly HashSet<int> _wanted = [];
    private readonly HashSet<int> _entered = [];
    private readonly HashSet<long> _levelsTried = [];
    private readonly Dictionary<long, HashSet<int>> _leftElements = [];
    private readonly HarvestSightings _sightings;
    private readonly GraphicSkills _graphics;
    private readonly Regrowths? _regrowths;
    private readonly TimeProvider _time;
    private readonly HarvestShutMaps _shut;
    private Func<int, int>? _levelOf;
    private int _harvests;
    private int _noWays;
    private int _doneAtArrival = -1;

    /// <summary>Gets the tour's number, from 1.</summary>
    public int Round { get; private set; } = 1;

    /// <summary>Gets whether anything was harvested in this tour.</summary>
    public bool HasHarvested { get; private set; }

    /// <summary>Gets how many journeys in a row arrived with nothing done since the one before: no harvest, and no map gone through, left out, crossed or zone entered (D488).</summary>
    public int IdleArrivals { get; private set; }

    /// <summary>Gets the vertices of the world graph, zones of maps already gone through, where a wanted resource was left that the character's own zone could not reach (D425).</summary>
    public IReadOnlySet<int> OtherZones => _wanted;

    /// <summary>Gets the part of a map gone through that the tour comes onto from a neighbouring map, for wanted resources its own part could not reach (D495).</summary>
    public MapLevel? OtherLevel { get; private set; }

    /// <summary>Gets how many maps this tour went through.</summary>
    public int Visited => _visited.Count;

    /// <summary>Initializes a new instance of the <see cref="HarvestTour"/> class.</summary>
    /// <param name="sightings">What the host's bots saw of the maps.</param>
    /// <param name="graphics">What the host learned of the maps' graphics.</param>
    /// <param name="regrowths">What the host learned of the resources' growing back, if it learns.</param>
    /// <param name="time">The bots' clock.</param>
    /// <param name="shut">The maps the activity's tours leave out for what the character is, kept beyond this tour (D581); none, this tour's own.</param>
    public HarvestTour(HarvestSightings sightings, GraphicSkills graphics, Regrowths? regrowths = null, TimeProvider? time = null, HarvestShutMaps? shut = null)
    {
        _sightings = sightings;
        _graphics = graphics;
        _regrowths = regrowths;
        _time = time ?? TimeProvider.System;
        _shut = shut ?? new HarvestShutMaps();
    }

    /// <summary>Takes the character's levels, which the tour weighs: a resource above the level of its profession makes no map probable, and no interior worth entering (D417).</summary>
    /// <param name="state">The character's state; one whose professions the server has not described yet leaves the levels unweighed.</param>
    /// <remarks>
    /// A level gained, of the character or of a profession, a subscription taken or a zaap learned may open a way the graph's criteria
    /// or its teleports shut: the maps no way led to are sought again. The objects and kamas carried, which criteria read too, are not
    /// watched: each harvest changes them, and with them the maps would be sought at every one.
    /// </remarks>
    public void Know(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _levelOf = state.Jobs.Count is 0 ? null : job => MapHarvest.LevelOf(state, job);
        _shut.Know(state, _time.GetUtcNow());
    }

    /// <summary>Gets how many maps a tour goes through: those not left out, nor out of every way.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public int Total(HarvestZone zone)
    {
        var toFind = ToFind(zone);

        return zone.Maps.Keys.Count(map => !_shut.Contains(map) && !IsLeftOut(map, zone, toFind));
    }

    /// <summary>Gets how many maps of the zone the tours leave out for what the character is, no way leading to them or a group having beaten it there (D582), that a tour would go through otherwise: those not left out for what they hold.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public int Shut(HarvestZone zone)
    {
        var toFind = ToFind(zone);

        return zone.Maps.Keys.Count(map => _shut.Contains(map) && !IsLeftOut(map, zone, toFind));
    }

    /// <summary>Gets how many maps of the zone are probable.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public int Probable(HarvestZone zone)
    {
        return zone.Maps.Keys.Count(map => IsProbable(map, zone));
    }

    /// <summary>Gets the maps still to go through in this tour: the probable ones first, then those of the world map, then the insides.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>The zone's maps neither gone through, nor failed to reach, nor left out, of the first kind that has some.</returns>
    /// <remarks>
    /// Measured on the pack of 3.7.12.12: 114 of the 186 sub-areas listing resources hold insides too, 1,493 maps against
    /// 6,569, mostly houses; but the mines are insides of such sub-areas, so a tour keeps them, last unless probable. Doors
    /// cost a change each way, and a first tour through a forest's houses harvested nothing for minutes, measured.
    /// </remarks>
    public HashSet<long> Remaining(HarvestZone zone)
    {
        var now = _time.GetUtcNow();
        var toFind = ToFind(zone);
        HashSet<long> remaining = [.. zone.Maps.Keys.Where(map => !_visited.Contains(map) && !_unreachable.Contains(map) && !_shut.Contains(map) && !IsLeftOut(map, zone, toFind) && !(ReadyAt(map, zone) > now))];

        // The isolated maps a tour goes through come last, whatever they hold: the way there is long (D149).
        HashSet<long> near = [.. remaining.Where(map => !zone.Isolated.Contains(map))];

        if (near.Count is 0)
            return remaining;

        HashSet<long> probable = [.. near.Where(map => IsProbable(map, zone))];

        if (probable.Count > 0)
            return probable;

        return near.Any(map => !zone.Insides.Contains(map)) ? [.. near.Where(map => !zone.Insides.Contains(map))] : near;
    }

    /// <summary>Gets how many maps of the zone still grow back.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public int Growing(HarvestZone zone)
    {
        var now = _time.GetUtcNow();
        var toFind = ToFind(zone);

        return zone.Maps.Keys.Count(map => !IsLeftOut(map, zone, toFind) && ReadyAt(map, zone) > now);
    }

    /// <summary>Gets when the first map this tour still goes through should have grown back, while every one still grows back.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>When, or <see langword="null"/> when none grows back.</returns>
    public DateTimeOffset? NextReady(HarvestZone zone)
    {
        var now = _time.GetUtcNow();
        var toFind = ToFind(zone);
        DateTimeOffset? first = null;

        foreach (var map in zone.Maps.Keys)
        {
            if (_visited.Contains(map) || _unreachable.Contains(map) || _shut.Contains(map) || IsLeftOut(map, zone, toFind) || ReadyAt(map, zone) is not { } ready || ready <= now)
                continue;

            if (first is null || ready < first)
                first = ready;
        }

        return first;
    }
    /// <summary>Gets how many maps of the zone are isolated, and left out unless the settings take them.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public static int Isolated(HarvestZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return zone.Isolated.Count;
    }

    /// <summary>Marks a map gone through.</summary>
    /// <param name="mapId">The map.</param>
    public void Visit(long mapId)
    {
        _visited.Add(mapId);
    }

    /// <summary>Gets whether a map was gone through in this tour.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Whether it was.</returns>
    public bool IsVisited(long mapId)
    {
        return _visited.Contains(mapId);
    }

    /// <summary>Marks a map out of the zone harvested on the way: the journeys of this tour no longer stop on it (D439).</summary>
    /// <param name="mapId">The map.</param>
    /// <remarks>Kept apart from the maps gone through, which count the tour's progress: a map out of the zone is no map of the tour.</remarks>
    public void Cross(long mapId)
    {
        _crossed.Add(mapId);
    }

    /// <summary>Gets whether a map out of the zone was harvested on the way in this tour.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Whether it was.</returns>
    public bool IsCrossed(long mapId)
    {
        return _crossed.Contains(mapId);
    }

    /// <summary>Marks an element of a map whose use failed in this tour: no journey of the tour stops for it again (D507).</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="elementId">The element.</param>
    public void LeaveElement(long mapId, int elementId)
    {
        if (!_leftElements.TryGetValue(mapId, out var elements))
            _leftElements[mapId] = elements = [];

        elements.Add(elementId);
    }

    /// <summary>Gets the elements of a map whose use failed in this tour.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>The elements, by id.</returns>
    public IReadOnlySet<int> ElementsLeftOn(long mapId)
    {
        return _leftElements.TryGetValue(mapId, out var elements) ? elements : FrozenSet<int>.Empty;
    }

    /// <summary>Marks a map the way to failed: left out of the rest of the tour.</summary>
    /// <param name="mapId">The map.</param>
    public void Unreach(long mapId)
    {
        _unreachable.Add(mapId);
    }

    /// <summary>Marks a map no way of the world graph leads to from where the character stands, by its rules: left out of this tour and the next ones, until a level, the subscription or the zaaps known change (<see cref="Know"/>).</summary>
    /// <param name="mapId">The map.</param>
    /// <remarks>
    /// Measured on the owner's farmer of 25 and 26 November: a sub-area of 34 maps, one the character could reach, the others behind a
    /// door's criterion. A new tour sought them again, the journey found no way, and so on every second until a group of the map
    /// attacked the character, which stood still all the while. A journey that failed is no such fact: <see cref="Unreach"/>.
    /// </remarks>
    public void NoWay(long mapId)
    {
        if (_shut.NoWay(mapId))
            _noWays++;
    }

    /// <summary>Gets whether the character stood in a zone, or gave it up, in this tour.</summary>
    /// <param name="vertex">The zone's vertex in the world graph.</param>
    /// <returns>Whether it did.</returns>
    public bool IsEntered(int vertex)
    {
        return _entered.Contains(vertex);
    }

    /// <summary>Wants a zone of a map: the tour goes to it once, as one more place to go through beside the maps (D425).</summary>
    /// <param name="vertex">The zone's vertex in the world graph. One the character stood in or gave up in this tour is not wanted again.</param>
    public void WantZone(int vertex)
    {
        if (!_entered.Contains(vertex))
            _wanted.Add(vertex);
    }

    /// <summary>Marks a zone the character stands in, so that this tour does not go there again.</summary>
    /// <param name="vertex">The zone's vertex in the world graph.</param>
    public void Enter(int vertex)
    {
        _entered.Add(vertex);
        _wanted.Remove(vertex);
    }

    /// <summary>Gives up a zone wanted that cannot be got to: left out of the rest of the tour.</summary>
    /// <param name="vertex">The zone's vertex in the world graph.</param>
    public void GiveUp(int vertex)
    {
        Enter(vertex);
    }

    /// <summary>Wants a part of a map: the tour comes onto it once, before its next journey (D495).</summary>
    /// <param name="level">The part. One of a map whose part was wanted already in this tour is not wanted again.</param>
    /// <returns>Whether the tour wants it.</returns>
    public bool WantLevel(MapLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (!_levelsTried.Add(level.MapId))
            return false;

        OtherLevel = level;

        return true;
    }

    /// <summary>Takes the part of a map wanted, which the tour then goes to: wanted no more, whether the way there arrives or not.</summary>
    /// <returns>The part, if one was wanted.</returns>
    public MapLevel? TakeLevel()
    {
        var level = OtherLevel;

        OtherLevel = null;

        return level;
    }

    /// <summary>Marks a harvest in this tour.</summary>
    public void Harvested()
    {
        HasHarvested = true;
        _harvests++;
    }

    /// <summary>Marks a harvest out of the zone, on the way: something done for <see cref="Arrived"/>, nothing of the zone's (D439, D507).</summary>
    /// <remarks>A journey may stop again on a map out of the zone the tour crossed already, for a resource grown back: crossing it again adds nothing to the tour, so the harvest must.</remarks>
    public void HarvestedOnTheWay()
    {
        _harvests++;
    }

    /// <summary>Counts a journey of the tour that arrived.</summary>
    /// <returns>Whether the tour goes round: <see cref="MostRounds"/> journeys in a row arrived with nothing done since the one before.</returns>
    /// <remarks>
    /// Measured in a mine (D488): each journey arrived on the map whose ore the character never reached, the server moving it on
    /// as it walked there; the map was never gone through, and the tour went round its eight maps 58 times in 18 minutes, nothing
    /// harvested. Every journey of a tour otherwise ends on a map it goes through, harvests on, or leaves.
    /// </remarks>
    public bool Arrived()
    {
        var done = _visited.Count + _crossed.Count + _unreachable.Count + _noWays + _entered.Count + _harvests;

        IdleArrivals = done == _doneAtArrival ? IdleArrivals + 1 : 0;
        _doneAtArrival = done;

        return IdleArrivals >= MostRounds;
    }

    /// <summary>Gives up every map and zone this tour still goes to: left out of the rest of the tour, which then ends.</summary>
    /// <param name="zone">The zone.</param>
    public void GiveUpRemaining(HarvestZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        foreach (var map in zone.Maps.Keys)
        {
            if (!_visited.Contains(map))
                _unreachable.Add(map);
        }

        foreach (var vertex in _wanted.ToList())
            Enter(vertex);
    }

    /// <summary>Begins the next tour, once this one went through every map.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>Whether a map of the zone may still hold a resource: a tour has something to go through.</returns>
    public bool Next(HarvestZone zone)
    {
        if (Total(zone) is 0)
            return false;

        _visited.Clear();
        _crossed.Clear();
        _unreachable.Clear();
        _wanted.Clear();
        _entered.Clear();
        _levelsTried.Clear();
        _leftElements.Clear();
        OtherLevel = null;
        HasHarvested = false;
        _harvests = 0;
        _doneAtArrival = -1;
        IdleArrivals = 0;
        Round++;

        return true;
    }

    /// <summary>Gets each map of the zone, with where the tour stands in it.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>The maps, for the app's map.</returns>
    public ImmutableArray<ZoneMap> Maps(HarvestZone zone)
    {
        var toFind = ToFind(zone);

        return [.. zone.Maps.Select(map => new ZoneMap(map.Key, StateOf(map.Key, zone, toFind), map.Value))];
    }

    private ZoneMapState StateOf(long mapId, HarvestZone zone, HashSet<int> toFind)
    {
        if (zone.Isolated.Contains(mapId) && !zone.IncludesIsolated)
            return ZoneMapState.Isolated;

        if (!IsLeftOut(mapId, zone, toFind) && ReadyAt(mapId, zone) > _time.GetUtcNow())
            return ZoneMapState.Growing;

        if (_visited.Contains(mapId))
            return ZoneMapState.Visited;

        if (IsLeftOut(mapId, zone, toFind))
            return ZoneMapState.Empty;

        if (_unreachable.Contains(mapId) || _shut.Contains(mapId))
            return ZoneMapState.Unreachable;

        return IsProbable(mapId, zone) ? ZoneMapState.Probable : ZoneMapState.ToVisit;
    }

    // A map whose wanted elements were all seen harvested is held back until the first should have grown back. Measured:
    // the server never says when; one harvested before it was seen grew back no later than the first sighting of it so
    // reckons, which later sightings must not put off (9g5), unless they find it still growing after.
    private DateTimeOffset? ReadyAt(long mapId, HarvestZone zone)
    {
        if (_regrowths is null || !_sightings.TryGet(mapId, out var sighting) || sighting.Elements.IsDefaultOrEmpty)
            return null;

        DateTimeOffset? first = null;

        foreach (var element in sighting.Elements)
        {
            if (!zone.Targets.ContainsKey(element.SkillId))
                continue;

            if (element.IsEnabled || _regrowths.Reckoned(element.SkillId) is not { } takes)
                return null;

            var harvested = _regrowths.TryGetHarvest(mapId, element.ElementId, out var at) ? at : element.DisabledSince ?? sighting.At;
            var ready = harvested + takes;

            // Seen still growing once it should have grown back, the reckoning was short for this one: kept as it was, the map was ready
            // again at every tour until it grew back, the owner's miner going back 18 to 30 min after its harvests (D583). The visit's
            // own bound from below makes the reckoning end at that very sighting: it holds the map back too.
            if (sighting.At >= ready)
                ready = sighting.At + takes / 2;

            if (first is null || ready < first)
                first = ready;
        }

        return first;
    }
    // What a bot saw of a map wins over what its graphics foretell: it is the server's word for that map.
    private bool IsLeftOut(long mapId, HarvestZone zone, HashSet<int> toFind)
    {
        if (zone.Isolated.Contains(mapId) && !zone.IncludesIsolated)
            return true;

        if (_sightings.TryGet(mapId, out var sighting))
            return zone.Interiors.Contains(mapId) ? !HoldsAResource(sighting, zone) : !sighting.Resources.Overlaps(zone.Wanted);

        // An outdoor map never seen is gone to, to learn what it holds; a house of a town is not: a player's report is that the bot went
        // into all of Astrub's, and a graphic nothing taught is a door or a piece of furniture more often than a resource (D416). But a
        // mine is an interior too, and its ore is learned only by entering: while its sub-area lists a resource no map of it showed, an
        // interior whose graphics are not all known is gone into, last (D444).
        if (!zone.Interiors.Contains(mapId))
            return zone.Foresee(mapId, _graphics, _levelOf) is MapForecast.Without;

        return zone.Foresee(mapId, _graphics, _levelOf) switch
        {
            MapForecast.Probable => false,
            MapForecast.Unknown => !toFind.Contains(zone.Maps[mapId]),
            _ => true,
        };
    }

    // The sub-areas of the zone, by index, that list a resource asked for, at the character's level, which no map of theirs was seen
    // holding nor is foretold to hold by a graphic learned elsewhere (D444). Measured on the pack of 3.7.14.18: 42 maps of Amakna's mines
    // are interiors of sub-areas that also hold maps on a world map, which D416 left out until a bot had learned their ore.
    private HashSet<int> ToFind(HarvestZone zone)
    {
        if (zone.Interiors.Count is 0)
            return [];

        var pending = new Dictionary<int, HashSet<int>>();

        foreach (var (index, items) in zone.Listed)
        {
            HashSet<int> usable = [.. items.Where(item => zone.Targets.Values.Any(target => target.ItemId == item && zone.CanUse(target.SkillId, _levelOf)))];

            if (usable.Count > 0)
                pending[index] = usable;
        }

        foreach (var (map, index) in zone.Maps)
        {
            // A map the tour leaves out for its way finds nothing: ore seen beyond it would keep the mine out while nothing reached holds it.
            if (!pending.TryGetValue(index, out var items) || items.Count is 0 || (zone.Isolated.Contains(map) && !zone.IncludesIsolated))
                continue;

            // What a bot saw of a map is the server's word for it; a map never seen is foretold by its graphics.
            if (_sightings.TryGet(map, out var sighting))
            {
                items.ExceptWith(sighting.Resources);

                continue;
            }

            if (!zone.Graphics.TryGetValue(map, out var drawn))
                continue;

            foreach (var gfxId in drawn)
            {
                if (!_graphics.TryGet(gfxId, out var skills))
                    continue;

                foreach (var skill in skills)
                {
                    if (zone.Targets.TryGetValue(skill, out var target))
                        items.Remove(target.ItemId);
                }
            }
        }

        return [.. pending.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key)];
    }

    private bool IsProbable(long mapId, HarvestZone zone)
    {
        return _sightings.TryGet(mapId, out var sighting)
            ? HoldsAResource(sighting, zone)
            : zone.Foresee(mapId, _graphics, _levelOf) is MapForecast.Probable;
    }

    // A map seen with its elements holds a resource when one is for the character's level; one seen by its resources alone, by them.
    private bool HoldsAResource(MapSighting sighting, HarvestZone zone)
    {
        if (sighting.Elements.IsDefaultOrEmpty)
            return sighting.Resources.Overlaps(zone.Wanted);

        return sighting.Elements.Any(element => zone.CanUse(element.SkillId, _levelOf));
    }
}
