using D3B.Bot.Assets;
using D3B.Bot.Routes;
using D3B.Data.World;
using D3B.Engine.World;

namespace D3B.Bot.Tests.Routes;

public sealed class RoutePlannerTests
{
    private const int SubAreaId = 95;

    // 1 - 2 - 3 - 4 in a line, each way; 5 stands alone; 6 only leads to 1 (a one-way door); 7 has two zones, 71 leading on to 3.
    private static RoutePlanner Planner()
    {
        List<(long From, long To)> scrolls = [(1, 2), (2, 1), (2, 3), (3, 2), (3, 4), (4, 3), (6, 1)];
        var maps = new SortedSet<long> { 1, 2, 3, 4, 5, 6 };
        var vertices = maps.Select(map => new WorldVertex(map, 1)).ToList();
        var index = vertices.Select((vertex, i) => (vertex.MapId, i)).ToDictionary(pair => pair.MapId, pair => pair.i);
        var edges = new List<WorldEdge>();
        var transitions = new List<WorldTransition>();

        foreach (var group in scrolls.GroupBy(scroll => (From: index[scroll.From], To: index[scroll.To])).OrderBy(group => group.Key))
        {
            edges.Add(new WorldEdge(group.Key.From, group.Key.To, transitions.Count, group.Count()));
            transitions.AddRange(group.Select(scroll => new WorldTransition(WorldTransitionType.Scroll, 0, 0, WorldTransition.None, scroll.To, WorldTransition.None, WorldTransition.None)));
        }

        var graph = new WorldGraph(vertices.ToArray(), edges.ToArray(), transitions.ToArray(), Array.Empty<string>());
        var positions = maps.Select(map => new MapPosition(map, (short)map, 0, SubAreaId, 1, 0, 1u << 3)).ToArray();
        var atlas = new WorldAtlas(positions, new[] { new SubArea(SubAreaId, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0) }, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>());

        return new RoutePlanner(new WorldPathfinder(graph, atlas));
    }

    [Fact]
    public void ThePathBetweenTwoAnchorsListsTheMapsItCrossesInOrder()
    {
        var leg = Planner().Find(1, 4);

        Assert.Equal(RouteLegKind.Path, leg.Kind);
        Assert.Equal([2L, 3L], leg.Between);
        Assert.True(leg.IsReachable);
    }

    [Fact]
    public void NeighboursHaveNothingBetweenThem()
    {
        var leg = Planner().Find(2, 3);

        Assert.Equal(RouteLegKind.Path, leg.Kind);
        Assert.Empty(leg.Between);
    }

    [Fact]
    public void AMapNoExitLeadsToIsUnreachableAndSoIsOneTheGraphLacks()
    {
        var planner = Planner();

        Assert.Equal(RouteLegKind.Unreachable, planner.Find(1, 5).Kind);
        Assert.Equal(RouteLegKind.Unreachable, planner.Find(5, 1).Kind);
        Assert.Equal(RouteLegKind.Unreachable, planner.Find(1, 99).Kind);
        Assert.Equal(RouteLegKind.Unreachable, planner.Find(1, 6).Kind);
        Assert.Equal(RouteLegKind.Path, planner.Find(6, 1).Kind);
    }

    [Fact]
    public void TheSameMapNeedsNoPath()
    {
        var leg = Planner().Find(3, 3);

        Assert.Equal(RouteLegKind.Same, leg.Kind);
        Assert.True(leg.IsReachable);
    }

    [Fact]
    public void ALoopOfThreeOrMoreAnchorsClosesOnTheFirstAndOtherCoursesDoNot()
    {
        var planner = Planner();
        long[] anchors = [1, 3, 4];

        Assert.Equal([(1L, 3L), (3L, 4L), (4L, 1L)], planner.Plan(anchors, RouteMode.Loop).Select(leg => (leg.From, leg.To)));
        Assert.Equal([(1L, 3L), (3L, 4L)], planner.Plan(anchors, RouteMode.OneWay).Select(leg => (leg.From, leg.To)));
        Assert.Equal([(1L, 3L)], planner.Plan([1, 3], RouteMode.Loop).Select(leg => (leg.From, leg.To)));
        Assert.Empty(planner.Plan([1], RouteMode.Loop));
        Assert.True(RoutePlanner.ClosesBack(RouteMode.Loop, 3));
        Assert.False(RoutePlanner.ClosesBack(RouteMode.RoundTrip, 3));
    }

    [Fact]
    public void WhenSeveralMapsShareCoordinatesTheOneWithMostWaysOutIsChosen()
    {
        var planner = Planner();

        Assert.Equal(2, planner.Primary([5, 2, 1]));
        Assert.Equal(7, planner.Primary([7, 8]));
        Assert.Throws<ArgumentException>(() => planner.Primary([]));
    }
}
