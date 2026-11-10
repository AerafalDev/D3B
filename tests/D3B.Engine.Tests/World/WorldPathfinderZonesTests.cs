using D3B.Engine.Criteria;
using D3B.Engine.World;

namespace D3B.Engine.Tests.World;

/// <summary>The zones of a map as the goal of a search: the graph has no edge between two zones of one map, so the way goes out and back (D425).</summary>
public sealed class WorldPathfinderZonesTests
{
    private const int East = 0;
    private const int West = 4;

    private static readonly WorldPathRules _subscribed = new() { Facts = new CriterionFacts { Level = 50, Subscribed = true } };

    // Map 1 in two zones, which share no edge: the first leads east to map 2, which leads back west into the second zone of
    // map 1; and a third map beyond map 2.
    private static TestWorld TwoZones()
    {
        return new TestWorld()
            .Scroll(1, 2, East, zone: 1).Scroll(2, 1, West, toZone: 2)
            .Scroll(1, 2, East, zone: 2).Scroll(2, 1, West, toZone: 1)
            .Scroll(2, 3, East).Scroll(3, 2, West);
    }

    private static int Vertex(WorldPathfinder pathfinder, long map, int zone = 1)
    {
        Assert.True(pathfinder.Graph.TryGetVertex(map, zone, out var vertex));

        return vertex;
    }

    private static long[] Maps(WorldPathfinder pathfinder, WorldPath path)
    {
        return [pathfinder.Graph.Vertices.Span[path.From].MapId, .. path.Steps.Select(step => pathfinder.Graph.Vertices.Span[step.To].MapId)];
    }

    [Fact]
    public void AnotherZoneOfTheStartsMapIsReachedByLeavingTheMap()
    {
        var pathfinder = TwoZones().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long>(), new HashSet<int> { Vertex(pathfinder, 1, 2) }, _subscribed, out var path));

        Assert.Equal([1, 2, 1], Maps(pathfinder, path));
        Assert.Equal(Vertex(pathfinder, 1, 2), path.To);
    }

    [Fact]
    public void TheStartsOwnVertexIsReachedAtOnce()
    {
        var pathfinder = TwoZones().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long>(), new HashSet<int> { Vertex(pathfinder, 1, 1) }, _subscribed, out var path));

        Assert.Empty(path.Steps);
    }

    [Fact]
    public void AMapSoughtStillEndsAtOnceOnAnyOfItsZones()
    {
        var pathfinder = TwoZones().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long> { 1 }, new HashSet<int>(), _subscribed, out var path));

        Assert.Empty(path.Steps);
    }

    [Fact]
    public void TheNearestOfMapsAndVerticesIsTaken()
    {
        var pathfinder = TwoZones().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long> { 3 }, new HashSet<int> { Vertex(pathfinder, 1, 2) }, _subscribed, out var path));
        Assert.Equal([1, 2, 1], Maps(pathfinder, path));

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 3), new HashSet<long> { 3 }, new HashSet<int> { Vertex(pathfinder, 1, 2) }, _subscribed, out var here));
        Assert.Empty(here.Steps);
    }

    [Fact]
    public void AVertexNoWayLeadsToIsNotReached()
    {
        // The second zone is entered from nowhere: map 2 leads only to the first.
        var pathfinder = new TestWorld().Scroll(1, 2, East, zone: 1).Scroll(2, 1, West).Scroll(1, 2, East, zone: 2).Pathfinder();

        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long>(), new HashSet<int> { Vertex(pathfinder, 1, 2) }, _subscribed, out _));
    }

    [Fact]
    public void AVertexOfASubAreaClosedToAccountsWithoutASubscriptionIsEnteredOnlyWhenSubscribed()
    {
        var pathfinder = TwoZones().Map(1, TestWorld.ClosedSubAreaId).Scroll(2, 4, East).Scroll(4, 2, West).Pathfinder();
        var free = new WorldPathRules { Facts = new CriterionFacts { Subscribed = false } };

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 4), new HashSet<long>(), new HashSet<int> { Vertex(pathfinder, 1, 2) }, _subscribed, out _));
        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 4), new HashSet<long>(), new HashSet<int> { Vertex(pathfinder, 1, 2) }, free, out _));
    }

    [Fact]
    public void ABannedTransitionClosesTheWayToAVertex()
    {
        var pathfinder = TwoZones().Pathfinder();
        var vertex = Vertex(pathfinder, 1, 2);

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long>(), new HashSet<int> { vertex }, _subscribed, out var path));

        var banned = _subscribed with { Banned = new HashSet<int> { path.Steps[1].TransitionIndex } };

        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long>(), new HashSet<int> { vertex }, banned, out _));
    }

    [Fact]
    public void NoGoalIsNeverReached()
    {
        var pathfinder = TwoZones().Pathfinder();

        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 1, 1), new HashSet<long>(), new HashSet<int>(), _subscribed, out _));
    }
}
