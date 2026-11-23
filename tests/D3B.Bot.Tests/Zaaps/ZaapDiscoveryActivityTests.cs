using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Zaaps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Zaaps;

public sealed class ZaapDiscoveryActivityTests
{
    private static async Task<(TestConnection Connection, BotActor Bot)> KnowingAsync(TestPlay play, long mapId, params long[] known)
    {
        var (connection, bot) = await play.EnterAsync(300, mapId);

        connection.Server(new ZaapKnownListEvent { MapIds = { known } });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    private static string[] Warnings(ZaapDiscoveryActivity discovery)
    {
        return [.. discovery.Report!.Warnings.Select(warning => warning.Format.In(CultureInfo.InvariantCulture))];
    }

    [Fact]
    public async Task TheDiscoveryWalksToAZaapTheCharacterDoesNotKnowUntilTheServerListsIt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.MapId, TestPlay.NorthMapId, TestPlay.MapId);
        var discovery = new ZaapDiscoveryActivity();
        var run = bot.StartActivity(discovery);

        // The east map by its map action: the inside's zaap is unactivated, the away one out of the graph.
        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        Assert.Contains(new ZoneMap(TestPlay.EastMapId, ZoneMapState.ToVisit), discovery.Report!.Focus!.Zone);
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));
        connection.Server(new ZaapKnownListEvent { MapIds = { TestPlay.NorthMapId, TestPlay.MapId, TestPlay.EastMapId } });

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal((1, 2), (discovery.Report!.Progress!.Done, discovery.Report.Progress.Total));
        Assert.Equal(["The zaap of {0} is out of reach"], Warnings(discovery));
        Assert.Equal([new ZoneMap(TestPlay.AwayZaapMapId, ZoneMapState.Unreachable), new ZoneMap(TestPlay.EastMapId, ZoneMapState.Visited)], discovery.Report.Focus!.Zone);
    }

    [Fact]
    public async Task TheNearestZaapGoesFirst()
    {
        // From the north map, the first map's zaap is one change away, the east map's two.
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.NorthMapId, TestPlay.NorthMapId);
        var run = bot.StartActivity(new ZaapDiscoveryActivity());

        Assert.True(await TestWalks.WalkedAsync(play, connection, 1) >= 532);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.MapId, 20);
        connection.Server(new ZaapKnownListEvent { MapIds = { TestPlay.NorthMapId, TestPlay.MapId } });

        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 3, new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        Assert.Contains(play.Log.GetSnapshot(), record => record.Message.Contains($"knows the zaap of map {TestPlay.MapId}", StringComparison.Ordinal));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AZaapTheServerDoesNotListOnItsMapIsGivenUpAndToldApart()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.MapId, TestPlay.NorthMapId, TestPlay.MapId);
        var discovery = new ZaapDiscoveryActivity();
        var run = bot.StartActivity(discovery);

        await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(0, discovery.Report!.Progress!.Done);
        Assert.Equal(["The zaap of {0} is out of reach", "The zaap of {0} was not learnt on its map"], Warnings(discovery));
        Assert.Single(connection.Sent, request => request is MapMovementRequest);
    }

    [Fact]
    public async Task ACharacterThatKnowsEveryZaapItCanReachEndsTheDiscoveryAtOnce()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.MapId, TestPlay.NorthMapId, TestPlay.MapId, TestPlay.EastMapId);
        var discovery = new ZaapDiscoveryActivity();
        var run = bot.StartActivity(discovery);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.DoesNotContain(connection.Sent, request => request is MapMovementRequest);
        Assert.Equal(0, discovery.Report!.Progress!.Done);
    }

    [Fact]
    public async Task ACharacterInAnothersHavenBagFailsTheDiscoveryInsteadOfSteppingForever()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingAsync(play, TestPlay.MapId, TestPlay.NorthMapId);
        var information = TestPlay.Information(324, TestPlay.HavenBagMapId);

        information.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestPlay.CharacterId + 1 } };
        connection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        connection.Server(information);
        await bot.ProcessedAsync();

        var run = bot.StartActivity(new ZaapDiscoveryActivity());

        Assert.Equal(ActivityOutcome.Failed, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Empty(connection.Sent);
    }
}
