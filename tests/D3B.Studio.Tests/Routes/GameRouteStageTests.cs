using System.Globalization;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.Studio.Tests.Routes;

public sealed class GameRouteStageTests : IAsyncDisposable
{
    private readonly Behaviors.TestBehaviors _behaviors = new();
    private readonly Notes _control = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    private NodeCall Call(string id, object values)
    {
        Assert.True(_behaviors.Nodes.TryGet(id, out var definition));

        return RouteActionCalls.Make(definition, values);
    }

    private Task<bool> Play(BotActor bot, NodeCall action, long? next = null)
    {
        return bot.RunAsync(
            (context, token) =>
            {
                var view = new BehaviorView(context.State, true, 0, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
                var stage = new GameRouteStage(new ActionContext(context, view, _control), context, _behaviors.Nodes);

                return stage.PlayAsync(action, next, token);
            },
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AForcedExitPlaysAsAScrollTowardsTheSideAndTheRouteGoesOnOnceTheCharacterIsThere()
    {
        var play = _behaviors.Play;
        var (connection, bot) = await play.EnterAsync(433);
        var played = Play(bot, Call(RouteActions.Exit, new RouteExitValues { Direction = RouteDirection.Top }));

        Assert.Equal(27, await TestWalks.WalkedAsync(play, connection, 1));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(2));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 538);

        Assert.True(await TestWalks.AdvanceUntilDoneAsync(play.Time, played));
        Assert.Empty(_control.Texts);
    }

    [Fact]
    public async Task AnActionThatCannotBePlayedIsSaidInTheJournalAndFailsTheRoute()
    {
        var play = _behaviors.Play;
        var (connection, bot) = await play.EnterAsync(433);
        var previous = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        try
        {
            // The test map has no neighbour to the west.
            Assert.False(await Play(bot, Call(RouteActions.Exit, new RouteExitValues { Direction = RouteDirection.Left })));
            Assert.Equal([$"The action route.exit of the route could not be played on map {TestPlay.MapId} (NoPath): the route fails."], _control.Texts);
            Assert.Empty(connection.Sent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task ADoorWithoutACellFailsTheRouteBeforeAnythingIsSent()
    {
        var play = _behaviors.Play;
        var (connection, bot) = await play.EnterAsync(433);

        Assert.False(await Play(bot, Call(RouteActions.Door, new RouteDoorValues())));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AZaapIsTakenTowardsTheNextStepWhenOneSavesAChange()
    {
        var play = _behaviors.Play;
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId, TestPlay.SubscribedAccount);

        connection.Server(new ZaapKnownListEvent { MapIds = { TestPlay.EastMapId } });
        connection.Server(new KamasUpdateEvent { Kamas = 500 });
        await bot.ProcessedAsync();

        var played = Play(bot, Call(RouteActions.Zaap, new NoValues()), TestPlay.EastMapId);

        Assert.Equal(new TeleportRequest { MapId = TestPlay.EastMapId, Source = TeleporterType.Map }, await connection.SentAsync(1));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Server(new KamasUpdateEvent { Kamas = 490 });
        connection.Server(new DialogLeaveEvent { Type = DialogType.Teleport });
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.True(await TestWalks.AdvanceUntilDoneAsync(play.Time, played));
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
    }

    [Fact]
    public async Task AZaapWithNoNextStepOrNoneThatSavesAChangeTakesNoneAndSaysSo()
    {
        var play = _behaviors.Play;
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);

        Assert.True(await Play(bot, Call(RouteActions.Zaap, new NoValues())));
        Assert.True(await Play(bot, Call(RouteActions.Zaap, new NoValues()), TestPlay.EastMapId));
        Assert.Equal(2, _control.Texts.Count);
        Assert.Empty(connection.Sent);
    }

    private sealed class Notes : IBehaviorControl
    {
        public List<string> Texts { get; } = [];

        public void Pause()
        {
        }

        public void Stop()
        {
        }

        public void Note(string text)
        {
            Texts.Add(text);
        }
    }
}
