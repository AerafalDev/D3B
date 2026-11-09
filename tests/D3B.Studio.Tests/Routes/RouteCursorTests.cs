using D3B.Bot.Characters;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.Studio.Tests.Routes;

public sealed class RouteCursorTests
{
    private static List<int> Visited(RouteCursor cursor, int most)
    {
        var visited = new List<int>();

        while (!cursor.IsDone && visited.Count < most)
        {
            visited.Add(cursor.Anchor);
            cursor.Advance();
        }

        return visited;
    }

    [Fact]
    public void ALoopGoesRoundItsAnchorsForEverAndCountsItsLaps()
    {
        var cursor = new RouteCursor(3, RouteMode.Loop);

        Assert.Equal([0, 1, 2, 0, 1, 2, 0], Visited(cursor, 7));
        Assert.False(cursor.IsDone);
        Assert.Equal((7L, 2L), (cursor.Visits, cursor.Laps));
    }

    [Fact]
    public void ALoopOfTwoGoesAndComesBack()
    {
        Assert.Equal([0, 1, 0, 1], Visited(new RouteCursor(2, RouteMode.Loop), 4));
    }

    [Fact]
    public void AOneWayRouteGoesThroughItsAnchorsOnceAndIsDone()
    {
        var cursor = new RouteCursor(3, RouteMode.OneWay);

        Assert.Equal([0, 1, 2], Visited(cursor, 10));
        Assert.True(cursor.IsDone);
        Assert.Equal(-1, cursor.Anchor);
    }

    [Fact]
    public void ARouteThereAndBackGoesToTheLastAnchorAndBackToTheFirstOnce()
    {
        var cursor = new RouteCursor(4, RouteMode.RoundTrip);

        Assert.Equal([0, 1, 2, 3, 2, 1, 0], Visited(cursor, 20));
        Assert.True(cursor.IsDone);
    }

    [Fact]
    public void ASingleAnchorIsVisitedOnceWhateverTheCourseAndNoAnchorIsDoneAtOnce()
    {
        Assert.Equal([0], Visited(new RouteCursor(1, RouteMode.Loop), 5));
        Assert.Equal([0], Visited(new RouteCursor(1, RouteMode.RoundTrip), 5));
        Assert.True(new RouteCursor(0, RouteMode.Loop).IsDone);
    }

    [Fact]
    public void TheActionsPlayedAreCountedForTheAnchorAndStartOverAtTheNext()
    {
        var cursor = new RouteCursor(2, RouteMode.OneWay);

        cursor.ActionPlayed();
        cursor.ActionPlayed();

        Assert.Equal(2, cursor.Played);

        cursor.Advance();

        Assert.Equal((1, 0), (cursor.Anchor, cursor.Played));
    }

    [Fact]
    public void ARunKeepsItsCursorAcrossInterruptionsAndAnotherRunStartsOver()
    {
        var runs = new RouteRuns();
        var route = new RouteAsset { Id = "r1", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }] };
        var character = new CharacterKey(1, 2);
        var start = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

        runs.Of(character, route, start).Advance();

        Assert.Equal(1, runs.Of(character, route, start).Anchor);
        Assert.Equal(0, runs.Of(character, route, start.AddMinutes(5)).Anchor);
        Assert.Equal(0, runs.Of(new CharacterKey(1, 3), route, start).Anchor);

        runs.Of(character, route, start).Advance();
        runs.Forget(character, "r1");

        Assert.Equal(0, runs.Of(character, route, start).Anchor);
    }
}
