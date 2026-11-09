using System.Collections.Concurrent;
using System.Collections.Immutable;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.World;
using D3B.Studio.Assets;

namespace D3B.Studio.Routes;

/// <summary>Finds the maps a route crosses between its anchors, by the world's path (D299).</summary>
/// <remarks>
/// <para>
/// The path is the one the world graph gives with nothing assumed of the character but a subscription: a door or a scroll
/// with a criterion nothing answers stays closed. Every zaap of the game is assumed known, the route being made for any
/// character, and one is taken when it saves at least <see cref="ZaapSaving"/> map changes (D378); the run takes the
/// path again with what the character it plays on knows. The quickest from any zone of one map to any zone of the other is kept.
/// </para>
/// <para>
/// A leg found is kept by its two maps, so that moving one anchor searches again only the two legs it touches. Safe from any
/// number of threads: a search keeps its state in its own arrays.
/// </para>
/// </remarks>
public sealed class RoutePlanner
{
    /// <summary>How many map changes a zaap must save to be taken in the editor's path: the activities' own default (D378).</summary>
    public const int ZaapSaving = 5;

    private const int KeptLegs = 8192;
    private const int PrimaryCandidates = 64;

    private readonly WorldPathRules _rules;
    private readonly WorldPathfinder _pathfinder;
    private readonly ConcurrentDictionary<(long From, long To), RouteLeg> _legs = new();

    /// <summary>Initializes a new instance of the <see cref="RoutePlanner"/> class.</summary>
    /// <param name="pathfinder">The pathfinder over the world graph.</param>
    public RoutePlanner(WorldPathfinder pathfinder)
    {
        ArgumentNullException.ThrowIfNull(pathfinder);

        _pathfinder = pathfinder;
        _rules = new WorldPathRules
        {
            Facts = new CriterionFacts { Subscribed = true },
            KnownZaaps = pathfinder.Atlas.Waypoints.ToArray().Select(waypoint => waypoint.MapId).ToHashSet(),
            Kamas = long.MaxValue,
            TeleportChanges = ZaapSaving,
        };
    }

    /// <summary>Tells whether a route closes on its first anchor: a loop of three anchors or more (two go and come back).</summary>
    /// <param name="mode">How the route goes through its anchors.</param>
    /// <param name="anchors">How many anchors it has.</param>
    /// <returns>Whether the last anchor leads back to the first.</returns>
    public static bool ClosesBack(RouteMode mode, int anchors)
    {
        return mode == RouteMode.Loop && anchors > 2;
    }

    /// <summary>Finds the way from a map to another.</summary>
    /// <param name="from">The map left.</param>
    /// <param name="to">The map reached.</param>
    /// <returns>The leg: the maps crossed between, else that none leads, which a map the graph does not hold gives.</returns>
    public RouteLeg Find(long from, long to)
    {
        if (_legs.TryGetValue((from, to), out var known))
            return known;

        var leg = Search(from, to);

        if (_legs.Count >= KeptLegs)
            _legs.Clear();

        _legs[(from, to)] = leg;

        return leg;
    }

    /// <summary>Finds the legs of a route: one between each anchor and the next, and one closing a loop.</summary>
    /// <param name="maps">The anchors' maps, in order.</param>
    /// <param name="mode">How the route goes through them.</param>
    /// <returns>The legs in order; none for fewer than two anchors.</returns>
    public IReadOnlyList<RouteLeg> Plan(IReadOnlyList<long> maps, RouteMode mode)
    {
        ArgumentNullException.ThrowIfNull(maps);

        var legs = new List<RouteLeg>(maps.Count);

        for (var i = 0; i + 1 < maps.Count; i++)
            legs.Add(Find(maps[i], maps[i + 1]));

        if (ClosesBack(mode, maps.Count))
            legs.Add(Find(maps[^1], maps[0]));

        return legs;
    }

    /// <summary>Chooses among maps that share coordinates the one a paint or a click that cannot ask lands on: the one the world leaves the most ways from.</summary>
    /// <param name="maps">The maps on the coordinates, the world's own first.</param>
    /// <returns>The map; the first when none is in the graph.</returns>
    /// <exception cref="ArgumentException">There is no map.</exception>
    public long Primary(ReadOnlySpan<long> maps)
    {
        if (maps.IsEmpty)
            throw new ArgumentException("There is no map to choose.", nameof(maps));

        var best = maps[0];
        var most = 0;

        foreach (var map in maps[..Math.Min(maps.Length, PrimaryCandidates)])
        {
            var ways = 0;

            foreach (var vertex in VerticesOf(map))
                ways += _pathfinder.Graph.EdgesFrom(vertex).Length;

            if (ways > most)
            {
                best = map;
                most = ways;
            }
        }

        return best;
    }

    private RouteLeg Search(long from, long to)
    {
        if (from == to)
            return new RouteLeg(from, to, RouteLegKind.Same, []);

        var target = new HashSet<long> { to };
        WorldPath? shortest = null;

        foreach (var start in VerticesOf(from))
        {
            if (_pathfinder.TryFind(start, target, _rules, out var path) && (shortest is null || path.Steps.Count < shortest.Steps.Count))
                shortest = path;
        }

        if (shortest is null)
            return new RouteLeg(from, to, RouteLegKind.Unreachable, []);

        var between = ImmutableArray.CreateBuilder<long>();
        var zaaps = ImmutableArray.CreateBuilder<long>();
        var vertices = _pathfinder.Graph.Vertices.Span;
        var last = from;

        foreach (var step in shortest.Steps)
        {
            var map = vertices[step.To].MapId;

            if (step.IsTeleport)
                zaaps.Add(map);

            if (map != last && map != to)
                between.Add(map);

            last = map;
        }

        return new RouteLeg(from, to, RouteLegKind.Path, between.ToImmutable(), zaaps.ToImmutable());
    }

    private List<int> VerticesOf(long map)
    {
        var vertices = new List<int>(2);

        for (var zone = 1; zone <= WorldVertex.MaxZone; zone++)
        {
            if (_pathfinder.Graph.TryGetVertex(map, zone, out var vertex))
                vertices.Add(vertex);
        }

        return vertices;
    }
}
