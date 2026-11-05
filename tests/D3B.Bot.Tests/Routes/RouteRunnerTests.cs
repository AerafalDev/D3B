using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using D3B.Bot.Movement;
using D3B.Bot.Routes;

namespace D3B.Bot.Tests.Routes;

public sealed class RouteRunnerTests
{
    private static NodeCall Action(string node)
    {
        return new NodeCall(node, ImmutableSortedDictionary<string, JsonNode?>.Empty);
    }

    private static RouteAsset Route(RouteMode mode, RouteUnreachable unreachable, params (long Map, string[] Actions)[] anchors)
    {
        return new RouteAsset
        {
            Id = "r1",
            Name = "North",
            Mode = mode,
            Unreachable = unreachable,
            Anchors = [.. anchors.Select(anchor => new RouteAnchor { MapId = anchor.Map, Actions = [.. anchor.Actions.Select(Action)] })],
        };
    }

    [Fact]
    public async Task ARouteGoesToEachAnchorInOrderPlayingItsActionsAndIsDone()
    {
        var stage = new FakeStage();
        var route = Route(RouteMode.OneWay, RouteUnreachable.Skip, (1, ["wait", "wait"]), (2, []), (3, ["wait"]));

        var result = await RouteRunner.RunAsync(route, new RouteCursor(3, route.Mode), stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Done, result);
        Assert.Equal(["go 1", "play wait", "play wait", "go 2", "go 3", "play wait"], stage.Log);
    }

    [Fact]
    public async Task ARouteThereAndBackReturnsToTheFirstAnchorAndStops()
    {
        var stage = new FakeStage();
        var route = Route(RouteMode.RoundTrip, RouteUnreachable.Skip, (1, []), (2, []), (3, []));

        var result = await RouteRunner.RunAsync(route, new RouteCursor(3, route.Mode), stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Done, result);
        Assert.Equal(["go 1", "go 2", "go 3", "go 2", "go 1"], stage.Log);
    }

    [Fact]
    public async Task ALoopGoesOnUntilItIsStopped()
    {
        using var source = new CancellationTokenSource();
        FakeStage stage = null!;

        stage = new FakeStage { OnTravel = _ => { if (stage.Log.Count >= 8) source.Cancel(); } };

        var route = Route(RouteMode.Loop, RouteUnreachable.Skip, (1, []), (2, []), (3, []));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RouteRunner.RunAsync(route, new RouteCursor(3, route.Mode), stage, source.Token));

        Assert.Equal(["go 1", "go 2", "go 3", "go 1", "go 2", "go 3", "go 1", "go 2"], stage.Log);
    }

    [Fact]
    public async Task AMapNoPathReachesIsPassedAndSaidWhenTheRouteSaysSo()
    {
        var stage = new FakeStage { Unreachable = [2] };
        var route = Route(RouteMode.OneWay, RouteUnreachable.Skip, (1, []), (2, ["wait"]), (3, []));

        var result = await RouteRunner.RunAsync(route, new RouteCursor(3, route.Mode), stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Done, result);
        Assert.Equal(["go 1", "go 2", "note RouteUnreachablePassed", "go 3"], stage.Log);
    }

    [Fact]
    public async Task AMapNoPathReachesFailsTheRouteWhenTheRouteSaysSo()
    {
        var stage = new FakeStage { Unreachable = [2] };
        var route = Route(RouteMode.OneWay, RouteUnreachable.Fail, (1, []), (2, []), (3, []));

        var result = await RouteRunner.RunAsync(route, new RouteCursor(3, route.Mode), stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Failed, result);
        Assert.Equal(["go 1", "go 2", "note RouteUnreachableFails"], stage.Log);
    }

    [Fact]
    public async Task ALoopWhoseMapsNoPathReachesFailsAfterOneLapInsteadOfSpinning()
    {
        var stage = new FakeStage { Unreachable = [1, 2, 3] };
        var route = Route(RouteMode.Loop, RouteUnreachable.Skip, (1, []), (2, []), (3, []));

        var result = await RouteRunner.RunAsync(route, new RouteCursor(3, route.Mode), stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Failed, result);
        Assert.Equal("note RouteNothingReachable", stage.Log[^1]);
        Assert.Equal(3, stage.Log.Count(entry => entry.StartsWith("go ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AnActionTheRunDoesNotPlayFailsTheRouteBeforeItMoves()
    {
        var stage = new FakeStage();
        var route = Route(RouteMode.OneWay, RouteUnreachable.Skip, (1, []), (2, ["route.door"]));

        var result = await RouteRunner.RunAsync(route, new RouteCursor(2, route.Mode), stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Failed, result);
        Assert.Equal(["note RouteNotPlayed"], stage.Log);
    }

    [Fact]
    public async Task ACharacterThatCannotActWaitsAndTriesAgainThenGivesTheRunBackWhereItWas()
    {
        var stage = new FakeStage { Busy = true };
        var route = Route(RouteMode.OneWay, RouteUnreachable.Skip, (1, []), (2, []));
        var cursor = new RouteCursor(2, route.Mode);

        var result = await RouteRunner.RunAsync(route, cursor, stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Busy, result);
        Assert.Equal(10, stage.Log.Count(entry => entry == "wait"));
        Assert.Equal(0, cursor.Anchor);
    }

    [Fact]
    public async Task ARunCutInTheMiddleOfAnAnchorsActionsGoesOnFromTheNextOneNotTheFirst()
    {
        using var source = new CancellationTokenSource();
        var plays = 0;
        var stage = new FakeStage { OnPlay = _ => { if (++plays == 2) source.Cancel(); } };
        var route = Route(RouteMode.OneWay, RouteUnreachable.Skip, (1, ["wait", "wait", "wait"]), (2, []));
        var cursor = new RouteCursor(2, route.Mode);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RouteRunner.RunAsync(route, cursor, stage, source.Token));

        Assert.Equal((0, 1), (cursor.Anchor, cursor.Played));

        stage.OnPlay = null;
        stage.Log.Clear();

        var result = await RouteRunner.RunAsync(route, cursor, stage, TestContext.Current.CancellationToken);

        Assert.Equal(RouteResult.Done, result);
        Assert.Equal(["go 1", "play wait", "play wait", "go 2"], stage.Log);
    }

    private sealed class FakeStage : IRouteStage
    {
        public List<string> Log { get; } = [];

        public HashSet<long> Unreachable { get; init; } = [];

        public bool Busy { get; init; }

        public Action<long>? OnTravel { get; init; }

        public Action<NodeCall>? OnPlay { get; set; }

        public bool CanPlay(NodeCall action)
        {
            return action.Node == "wait";
        }

        public Task<TravelOutcome> TravelAsync(long map, CancellationToken cancellationToken)
        {
            if (Busy)
                return Task.FromResult(TravelOutcome.Busy);

            Log.Add("go " + map);
            OnTravel?.Invoke(map);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Unreachable.Contains(map) ? TravelOutcome.NoPath : TravelOutcome.Arrived);
        }

        public Task PlayAsync(NodeCall action, CancellationToken cancellationToken)
        {
            Log.Add("play " + action.Node);
            OnPlay?.Invoke(action);

            // A play cut before it ends is not counted, as the node's own is not.
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task WaitAsync(TimeSpan time, CancellationToken cancellationToken)
        {
            Log.Add("wait");

            return Task.CompletedTask;
        }

        public void Note(string key, params object[] values)
        {
            Log.Add("note " + key);
        }
    }
}
