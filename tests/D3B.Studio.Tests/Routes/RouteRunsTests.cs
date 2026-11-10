using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.Bot.Characters;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.Studio.Tests.Routes;

public sealed class RouteRunsTests
{
    private static readonly CharacterKey _first = new(1, 2);
    private static readonly CharacterKey _second = new(1, 3);

    private static RouteAsset Route(RouteMode mode, params int[] actions)
    {
        var action = new NodeCall("wait", ImmutableSortedDictionary<string, JsonNode?>.Empty);

        return new RouteAsset
        {
            Id = "r1",
            Name = "North",
            Mode = mode,
            Anchors = [.. actions.Select((count, index) => new RouteAnchor { MapId = index + 1, Actions = [.. Enumerable.Repeat(action, count)] })],
        };
    }

    [Fact]
    public void ACharacterFollowsARouteFromTheMomentItBeginsToTheMomentItEnds()
    {
        var runs = new RouteRuns();
        var route = Route(RouteMode.OneWay, 0, 0);
        var cursor = new RouteCursor(2, route.Mode);

        Assert.Null(runs.Position(_first));

        runs.Begin(_first, route, cursor);

        Assert.Equal("r1", runs.Position(_first)?.Route);

        runs.End(_first, cursor);

        Assert.Null(runs.Position(_first));
        Assert.Empty(runs.Positions());
    }

    [Fact]
    public void ThePositionSaysTheRoutesStepsAndWhereTheRunIsInThem()
    {
        var runs = new RouteRuns();
        var route = Route(RouteMode.Loop, 0, 3, 0);
        var cursor = new RouteCursor(3, route.Mode);

        runs.Begin(_first, route, cursor);
        cursor.Advance();
        cursor.Arrive();
        cursor.ActionPlayed();

        var position = runs.Position(_first)!;

        Assert.Equal(("North", 3, 1, true, 1, 3, 0L, false), (position.Name, position.Anchors, position.Anchor, position.Arrived, position.Played, position.Actions, position.Laps, position.Returning));
        Assert.Equal(2, position.Step);
        Assert.Equal(_first, position.Character);
    }

    [Fact]
    public void ALapOfALoopIsCountedFromOne()
    {
        var runs = new RouteRuns();
        var route = Route(RouteMode.Loop, 0, 0);
        var cursor = new RouteCursor(2, route.Mode);

        runs.Begin(_first, route, cursor);

        for (var visit = 0; visit < 5; visit++)
            cursor.Advance();

        Assert.Equal(3, runs.Position(_first)!.Lap);
    }

    [Fact]
    public void TwoCharactersOnTheSameRouteHaveEachTheirOwnPosition()
    {
        var runs = new RouteRuns();
        var route = Route(RouteMode.OneWay, 0, 0, 0);
        var one = new RouteCursor(3, route.Mode);
        var other = new RouteCursor(3, route.Mode);

        runs.Begin(_first, route, one);
        runs.Begin(_second, route, other);
        other.Advance();
        other.Advance();

        Assert.Equal(0, runs.Position(_first)!.Anchor);
        Assert.Equal(2, runs.Position(_second)!.Anchor);
        Assert.Equal(2, runs.Positions().Length);
    }

    [Fact]
    public void ARunThatEndsDoesNotTakeAwayTheRunThatBeganAfterIt()
    {
        var runs = new RouteRuns();
        var route = Route(RouteMode.OneWay, 0, 0);
        var old = new RouteCursor(2, route.Mode);
        var current = new RouteCursor(2, route.Mode);

        runs.Begin(_first, route, old);
        runs.Begin(_first, route, current);
        runs.End(_first, old);

        Assert.NotNull(runs.Position(_first));
    }

    [Fact]
    public void ARunThatIsDoneIsNoLongerFollowed()
    {
        var runs = new RouteRuns();
        var route = Route(RouteMode.OneWay, 0);
        var cursor = new RouteCursor(1, route.Mode);

        runs.Begin(_first, route, cursor);
        cursor.Advance();

        Assert.Null(runs.Position(_first));
    }

    [Fact]
    public void TheCursorOfARunSurvivesItsEndSoThatAnInterruptionGoesOnFromWhereItWasCut()
    {
        var runs = new RouteRuns();
        var route = Route(RouteMode.OneWay, 0, 0);
        var start = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        var cursor = runs.Of(_first, route, start);

        runs.Begin(_first, route, cursor);
        cursor.Advance();
        runs.End(_first, cursor);

        Assert.Equal(1, runs.Of(_first, route, start).Anchor);
    }
}
