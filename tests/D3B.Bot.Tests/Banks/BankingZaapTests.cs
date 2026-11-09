using D3B.Bot.Banks;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Banks;

public sealed class BankingZaapTests
{
    // The bank's inside holds a zaap in this test, so that teleporting there saves a change on foot; the east map is 10 kamas away.
    private static async Task<(TestConnection Connection, BotActor Bot)> OnTheEastMapWithAZaapToTheBankAsync(TestPlay play, long kamas)
    {
        var (connection, bot) = await play.EnterAsync(300, TestPlay.EastMapId, TestPlay.SubscribedAccount);

        connection.Server(new ZaapKnownListEvent { MapIds = { TestPlay.InsideMapId } });
        connection.Server(new InventoryContentEvent { Kamas = kamas });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    // On foot from the east map: west by the scroll, then the door into the bank's inside.
    private static async Task<BankOutcome> WalkToTheBankAsync(TestPlay play, TestConnection connection, Task<BankOutcome> journey, int sent)
    {
        await TestWalks.WalkedAsync(play, connection, sent + 1);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, sent + 2));
        TestWalks.Arrive(connection, TestPlay.MapId, 287);

        Assert.IsType<InteractiveUseRequest>(await TestWalks.SentAsync(play, connection, sent + 3));
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        return await TestWalks.AdvanceUntilDoneAsync(play.Time, journey);
    }

    [Fact]
    public async Task ACharacterWhoCannotPayTheZaapToTheBankWalksThere()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await OnTheEastMapWithAZaapToTheBankAsync(play, 5);
        var journey = bot.RunAsync((context, token) => context.GoToBankerAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal(BankOutcome.Done, await WalkToTheBankAsync(play, connection, journey, 0));
        Assert.DoesNotContain(connection.Sent, request => request is TeleportRequest);
    }

    [Fact]
    public async Task AZaapToTheBankTheServerRefusesIsWalkedInstead()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await OnTheEastMapWithAZaapToTheBankAsync(play, 500);
        var journey = bot.RunAsync((context, token) => context.GoToBankerAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal(new TeleportRequest { MapId = TestPlay.InsideMapId, Source = TeleporterType.Map }, await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new TextInformationEvent { TextId = 82, Type = TextInformationType.Error });

        Assert.Equal(BankOutcome.Done, await WalkToTheBankAsync(play, connection, journey, 1));
        Assert.Single(connection.Sent, request => request is TeleportRequest);
    }

    [Fact]
    public async Task ACharacterWithTheKamasTakesTheZaapToTheBank()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await OnTheEastMapWithAZaapToTheBankAsync(play, 500);
        var journey = bot.RunAsync((context, token) => context.GoToBankerAsync(token), TestContext.Current.CancellationToken);

        Assert.IsType<TeleportRequest>(await TestWalks.SentAsync(play, connection, 1));
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.Equal(BankOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
    }

    [Fact]
    public async Task ACharacterWhoMayTakeNoZaapToTheBankWalksThereWithTheKamas()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await OnTheEastMapWithAZaapToTheBankAsync(play, 500);
        var journey = bot.RunAsync((context, token) => context.GoToBankerAsync(false, token), TestContext.Current.CancellationToken);

        Assert.Equal(BankOutcome.Done, await WalkToTheBankAsync(play, connection, journey, 0));
        Assert.DoesNotContain(connection.Sent, request => request is TeleportRequest);
    }
}
