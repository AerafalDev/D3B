using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Zaaps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Zaaps;

public sealed class ZaapDiscoveryActivityTests
{
    private static async Task<(TestConnection Connection, BotActor Bot)> KnowingAsync(TestPlay play, params long[] known)
    {
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(new ZaapKnownListEvent { MapIds = { known } });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    [Fact]
    public async Task TheDiscoveryWalksToAZaapTheCharacterDoesNotKnowUntilTheServerListsIt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.NorthMapId);
        var discovery = new ZaapDiscoveryActivity();
        var run = bot.StartActivity(discovery);

        // The east map by its map action: the north map's zaap is known, the inside's unactivated, the away one out of the graph.
        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        Assert.Contains(new ZoneMap(TestPlay.EastMapId, ZoneMapState.ToVisit), discovery.Report!.Focus!.Zone);
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));
        connection.Server(new ZaapKnownListEvent { MapIds = { TestPlay.NorthMapId, TestPlay.EastMapId } });

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal((1, 2), (discovery.Report!.Progress!.Done, discovery.Report.Progress.Total));
        Assert.Single(discovery.Report.Warnings);
        Assert.Equal([new ZoneMap(TestPlay.AwayZaapMapId, ZoneMapState.Unreachable), new ZoneMap(TestPlay.EastMapId, ZoneMapState.Visited)], discovery.Report.Focus!.Zone);
    }

    [Fact]
    public async Task AZaapTheServerDoesNotListOnItsMapIsGivenUp()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.NorthMapId);
        var discovery = new ZaapDiscoveryActivity();
        var run = bot.StartActivity(discovery);

        await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(0, discovery.Report!.Progress!.Done);
        Assert.Equal(2, discovery.Report.Warnings.Length);
        Assert.Single(connection.Sent, request => request is MapMovementRequest);
    }

    [Fact]
    public async Task ACharacterThatKnowsEveryZaapItCanReachEndsTheDiscoveryAtOnce()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.NorthMapId, TestPlay.EastMapId);
        var discovery = new ZaapDiscoveryActivity();
        var run = bot.StartActivity(discovery);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.DoesNotContain(connection.Sent, request => request is MapMovementRequest);
        Assert.Equal(0, discovery.Report!.Progress!.Done);
    }
}
