using System.Diagnostics.CodeAnalysis;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.Maps;

namespace D3B.Engine.World;

/// <summary>Finds paths between maps over the client's world graph, and the cells each map is left from.</summary>
/// <remarks>
/// <para>
/// A vertex is a zone of a map, the cells of which reach one another (D27). The search follows scrolls, scrolls with an
/// action and map actions; interactive transitions, such as doors, are session 4c's, and zaaps 4e's (D64). A transition is
/// followed only when its criterion is met (D71): what the character cannot be sure of closes it. A sub-area the client
/// closes to accounts without a subscription is entered only by a subscribed account.
/// </para>
/// <para>
/// Every map change costs the same: the character walks across a map, then the client loads the next, a few seconds
/// either way (client.md), so the quickest path is the one with the fewest. Dijkstra's search, with no estimate: map
/// actions lead anywhere, so no distance between maps bounds what is left.
/// </para>
/// <para>
/// Built once per pack and shared: the criteria are read once and the vertices' sub-areas found once, and nothing changes
/// after. Each search keeps its state in its own arrays, so any number of bots search at once (D9).
/// </para>
/// </remarks>
public sealed class WorldPathfinder
{
    private const int ChangeCost = 1;

    private readonly Criterion?[] _criteria;
    private readonly int[] _subAreas;
    private readonly int[] _areas;
    private readonly bool[] _basic;

    /// <summary>Gets the world graph the paths go through.</summary>
    public WorldGraph Graph { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldPathfinder"/> class.</summary>
    /// <param name="graph">The world graph.</param>
    /// <param name="atlas">The atlas giving each map its sub-area, and each sub-area its area and whether accounts without a subscription enter it.</param>
    public WorldPathfinder(WorldGraph graph, WorldAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(atlas);

        var vertices = graph.Vertices.Span;

        Graph = graph;
        _criteria = new Criterion?[graph.Criteria.Length];
        _subAreas = new int[vertices.Length];
        _areas = new int[vertices.Length];
        _basic = new bool[vertices.Length];

        for (var i = 0; i < _criteria.Length; i++)
            _criteria[i] = Criterion.Parse(graph.Criteria.Span[i]);

        for (var i = 0; i < vertices.Length; i++)
        {
            var placed = atlas.TryGetPosition(vertices[i].MapId, out var position);
            var known = atlas.TryGetSubArea(position.SubAreaId, out var subArea) && placed;

            _subAreas[i] = placed ? position.SubAreaId : -1;
            _areas[i] = known ? subArea.AreaId : -1;
            _basic[i] = known && subArea.BasicAccountAllowed;
        }
    }

    private static bool LeavesTowards(MapCell mapCell, int cell, int direction)
    {
        if (mapCell.LeavesMapTowards(direction))
            return true;

        // A corner lies on two edges, which share its diagonal marks: only the straight one tells which edge it leaves by.
        var edges = 0;

        for (var straight = 0; straight < MapCell.DirectionCount; straight += 2)
        {
            if (MapGeometry.Neighbour(cell, straight) < 0)
                edges++;
        }

        return edges is 1
            && (mapCell.LeavesMapTowards((direction + 1) % MapCell.DirectionCount) || mapCell.LeavesMapTowards((direction + MapCell.DirectionCount - 1) % MapCell.DirectionCount));
    }

    private static int Distance(int from, int to)
    {
        var a = MapGeometry.PointOf(from);
        var b = MapGeometry.PointOf(to);

        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private static bool IsFollowedType(WorldTransitionType type)
    {
        return type is WorldTransitionType.Scroll or WorldTransitionType.ScrollAction or WorldTransitionType.MapAction;
    }

    private static int SourceOf(ReadOnlySpan<WorldEdge> edges, int transition)
    {
        // Edges hold their transitions in order: the last edge starting at or before the transition holds it.
        int low = 0, high = edges.Length - 1;

        while (low < high)
        {
            var middle = low + ((high - low + 1) >> 1);

            if (edges[middle].FirstTransition <= transition)
                low = middle;
            else
                high = middle - 1;
        }

        return edges[low].From;
    }

    /// <summary>Finds the vertex a character stands in.</summary>
    /// <param name="map">The map it is on.</param>
    /// <param name="cell">The cell it stands on.</param>
    /// <param name="vertex">The vertex, when the graph holds the cell's zone of the map.</param>
    /// <returns>Whether the graph holds it: a cell that cannot be walked on has no zone.</returns>
    public bool TryGetVertex(MapData map, int cell, out int vertex)
    {
        ArgumentNullException.ThrowIfNull(map);

        return Graph.TryGetVertex(map.Id, map.Cell(cell).Zone, out vertex);
    }

    /// <summary>Finds the path with the fewest map changes from a vertex to any zone of one of several maps.</summary>
    /// <param name="from">The vertex the character stands in.</param>
    /// <param name="maps">The maps sought.</param>
    /// <param name="rules">What the character may follow.</param>
    /// <param name="path">The path, when there is one: no step when the vertex's map is sought already.</param>
    /// <returns>Whether any of the maps can be reached.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The graph has no such vertex.</exception>
    public bool TryFind(int from, IReadOnlySet<long> maps, WorldPathRules rules, [NotNullWhen(true)] out WorldPath? path)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(from, Graph.Vertices.Length);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(rules);

        var vertices = Graph.Vertices.Span;
        var costs = new int[vertices.Length];
        var through = new int[vertices.Length];
        var closed = new bool[vertices.Length];
        var open = new PriorityQueue<int, int>();
        var reached = -1;

        Array.Fill(costs, int.MaxValue);
        costs[from] = 0;
        open.Enqueue(from, 0);

        while (open.TryDequeue(out var vertex, out _))
        {
            if (closed[vertex])
                continue;

            if (maps.Contains(vertices[vertex].MapId))
            {
                reached = vertex;

                break;
            }

            closed[vertex] = true;

            foreach (var edge in Graph.EdgesFrom(vertex))
            {
                if (closed[edge.To] || !MayEnter(edge.To, rules.Facts))
                    continue;

                var transition = Followed(vertex, edge, rules);
                var cost = costs[vertex] + ChangeCost;

                if (transition < 0 || cost >= costs[edge.To])
                    continue;

                costs[edge.To] = cost;
                through[edge.To] = transition;
                open.Enqueue(edge.To, cost);
            }
        }

        path = reached < 0 ? null : PathTo(from, reached, through);

        return path is not null;
    }

    /// <summary>Finds the cells a step's map is left from: where to walk before asking for the next map, or the cell to step on.</summary>
    /// <param name="cells">The cells of the step's map.</param>
    /// <param name="step">The step.</param>
    /// <param name="rules">What the character may follow: a criterion on the cell is answered for each.</param>
    /// <returns>The cells, none when no cell meets the transition's criterion.</returns>
    /// <remarks>
    /// <para>
    /// A map action is left from its own cell. A scroll is left from any walkable cell of the zone on the map's edge in its
    /// direction that the client marks as leaving it in that direction, or along a diagonal next to it but on a corner,
    /// and from the transition's own cell, which lies on that edge. The client left from such cells, one marked only along
    /// a diagonal among them; the server took a cell of the edge marked nowhere, and ignored a corner marked as leaving
    /// by the other edge (client.md).
    /// </para>
    /// <para>
    /// Where several scrolls leave the zone in one direction, to several zones or maps, a cell leads where the nearest of
    /// those whose criterion it may meet does; a criterion on the cell, such as <c>#(PC=4|PC=5)</c>, names the cells a
    /// scroll with an action is left from.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int> ExitCells(ReadOnlySpan<MapCell> cells, WorldStep step, WorldPathRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (cells.Length != MapData.CellCount)
            throw new ArgumentException($"A map has {MapData.CellCount} cells, not {cells.Length}.", nameof(cells));

        var transition = step.Transition;
        var facts = PlaceOf(step.From, rules.Facts);

        if (transition.Type is WorldTransitionType.MapAction or WorldTransitionType.Interactive)
            return Answer(transition, facts with { Cell = transition.CellId }) is CriterionAnswer.True ? [transition.CellId] : [];

        var zone = Graph.Vertices.Span[step.From].Zone;
        var direction = transition.Direction;
        var rivals = Rivals(step);
        var exits = new List<int>();

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var mapCell = cells[cell];

            if (!mapCell.IsWalkable || mapCell.Zone != zone || MapGeometry.Neighbour(cell, direction) >= 0)
                continue;

            if (cell != transition.CellId && !LeavesTowards(mapCell, cell, direction))
                continue;

            var at = facts with { Cell = cell };

            if (Answer(transition, at) is CriterionAnswer.True && Leads(step, rivals, cell, at))
                exits.Add(cell);
        }

        return exits;
    }

    private bool MayEnter(int vertex, CriterionFacts facts)
    {
        return facts.Subscribed is true || _basic[vertex];
    }

    private int Followed(int vertex, WorldEdge edge, WorldPathRules rules)
    {
        var transitions = Graph.TransitionsOf(edge);

        for (var i = 0; i < transitions.Length; i++)
        {
            var index = edge.FirstTransition + i;
            var transition = transitions[i];

            if (IsFollowedType(transition.Type) && !rules.Banned.Contains(index)
                && (transition.CriterionId is WorldTransition.None || Answer(transition, PlaceOf(vertex, rules.Facts)) is CriterionAnswer.True))
            {
                return index;
            }
        }

        return -1;
    }

    private CriterionAnswer Answer(WorldTransition transition, CriterionFacts facts)
    {
        return transition.CriterionId is WorldTransition.None ? CriterionAnswer.True : _criteria[transition.CriterionId]!.Evaluate(facts);
    }

    private CriterionFacts PlaceOf(int vertex, CriterionFacts facts)
    {
        return facts with
        {
            MapId = Graph.Vertices.Span[vertex].MapId,
            SubAreaId = _subAreas[vertex] < 0 ? null : _subAreas[vertex],
            AreaId = _areas[vertex] < 0 ? null : _areas[vertex],
            Cell = null,
        };
    }

    private List<(int Index, WorldTransition Transition)> Rivals(WorldStep step)
    {
        var rivals = new List<(int, WorldTransition)>();

        foreach (var edge in Graph.EdgesFrom(step.From))
        {
            var transitions = Graph.TransitionsOf(edge);

            for (var i = 0; i < transitions.Length; i++)
            {
                var index = edge.FirstTransition + i;
                var transition = transitions[i];

                if (index != step.TransitionIndex && (transition.Type is WorldTransitionType.Scroll or WorldTransitionType.ScrollAction)
                    && transition.Direction == step.Transition.Direction)
                {
                    rivals.Add((index, transition));
                }
            }
        }

        return rivals;
    }

    private bool Leads(WorldStep step, List<(int Index, WorldTransition Transition)> rivals, int cell, CriterionFacts at)
    {
        var distance = Distance(cell, step.Transition.CellId);

        foreach (var (index, rival) in rivals)
        {
            // A rival whose criterion is not known may still be the one the cell leads to.
            if (Answer(rival, at) is CriterionAnswer.False)
                continue;

            var rivalDistance = Distance(cell, rival.CellId);

            if (rivalDistance < distance || (rivalDistance == distance && index < step.TransitionIndex))
                return false;
        }

        return true;
    }

    private WorldPath PathTo(int from, int to, int[] through)
    {
        var steps = new List<WorldStep>();
        var transitions = Graph.Transitions.Span;
        var edges = Graph.Edges.Span;

        for (var vertex = to; vertex != from;)
        {
            var index = through[vertex];
            var source = SourceOf(edges, index);

            steps.Add(new WorldStep(source, vertex, index, transitions[index]));
            vertex = source;
        }

        steps.Reverse();

        return new WorldPath(from, steps);
    }
}
