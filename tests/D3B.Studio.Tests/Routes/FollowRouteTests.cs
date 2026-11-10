using D3B.Bot.Activities;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;
using static D3B.Studio.Tests.Behaviors.TestBehaviors;

namespace D3B.Studio.Tests.Routes;

public sealed class FollowRouteTests : IAsyncDisposable
{
    private readonly Behaviors.TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    [Fact]
    public async Task ACharacterIsFollowingItsRouteOnlyWhileTheRoutePlaysInTheListOfSteps()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(433);
        var route = new RouteAsset
        {
            Id = AssetLibrary.NewId(),
            Name = "Here",
            Anchors = [new RouteAnchor { MapId = bot.State.MapId!.Value, Actions = [new NodeCall("wait", Values(("Seconds", 60)))] }],
        };

        await _behaviors.Assets.SaveAsync(route, TestContext.Current.CancellationToken);

        var runs = (RouteRuns)_behaviors.Services.GetService(typeof(RouteRuns))!;
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("only", "Only") { Actions = Chain(Node("f", RouteActions.Follow, ("Route", route.Id)), Note("b", "after")) }));

        await UntilAsync(() => runs.Position(bot.State.Key) is { Arrived: true });

        var position = runs.Position(bot.State.Key)!;

        Assert.Equal((route.Id, 0, 0, 1), (position.Route, position.Anchor, position.Played, position.Actions));
        Assert.Equal(0, run.Activity.Report?.Progress?.Done);

        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(60));

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Null(runs.Position(bot.State.Key));
        Assert.Equal(2, Notes(bot).Count);
        Assert.Contains("Here", Notes(bot)[0], StringComparison.Ordinal);
        Assert.Equal("after", Notes(bot)[1]);
    }
}
