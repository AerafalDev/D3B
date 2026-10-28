using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class WorldTravelTests
{
    [Fact]
    public async Task AJourneyChangesMapsOneAfterAnotherUntilTheMapSought()
    {
        // From the north map: south by a scroll, then east by the map action.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        Assert.True(await TestWalks.WalkedAsync(play, connection, 1) >= 532);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.MapId, 20);

        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 3, new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Equal(3, connection.Sent.Count);
    }

    [Fact]
    public async Task AJourneyGoesThroughADoor()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(287);
        var journey = bot.TravelAsync(TestPlay.InsideMapId, TestContext.Current.CancellationToken);

        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ACharacterOnTheMapSoughtHasArrived()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        Assert.Equal(TravelOutcome.Arrived, await bot.TravelAsync(TestPlay.MapId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AMapNoPathLeadsToSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        Assert.Equal(TravelOutcome.NoPath, await bot.TravelAsync(42, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AMapChangeThatFailsIsLeftOutAndAnotherWayTaken()
    {
        // North by the scroll first, which the server ignores; then by the map action.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var journey = bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await connection.SentAsync(1));

        while (connection.Sent.Count < 2)
        {
            play.Time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(TestPlay.NorthActionCell, await TestWalks.WalkedAsync(play, connection, 2, new MapCurrentEvent { MapId = TestPlay.NorthMapId }));
        connection.Server(TestPlay.Information(552, TestPlay.NorthMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
    }

    [Fact]
    public async Task AJourneyWhoseOnlyWayFailsEndsWithNoPath()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(TestPlay.ActionCell + 1);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        // The server never shows the walk onto the action cell: the change fails, and no other way leads east.
        Assert.Equal(TravelOutcome.NoPath, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ACharacterThatWalksIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 405 } });
        await bot.ProcessedAsync();

        Assert.Equal(TravelOutcome.Busy, await bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public void TheFactsOfACharacterAreItsStateAtAMoment()
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var state = new CharacterState
        {
            Key = TestPlay.Key,
            Name = "Character",
            Level = 50,
            MapId = TestPlay.MapId,
            SubAreaId = TestPlay.SubAreaId,
            Cell = 300,
            Inventory = ImmutableSortedDictionary.CreateRange(new Dictionary<int, InventoryItem>
            {
                [1] = new(1, 289, 10, 63),
                [2] = new(2, 289, 5, 63),
                [3] = new(3, 1782, 1, 63),
            }),
            Jobs = ImmutableSortedDictionary.CreateRange(new Dictionary<int, CharacterJob> { [28] = new(28, 12, 0, 0, 0, []) }),
            SubscriptionEnd = now.AddDays(2),
        };

        var facts = WorldTravel.FactsOf(state, now);

        Assert.Equal(50, facts.Level);
        Assert.Equal(15, facts.Items![289]);
        Assert.Equal(1, facts.Items[1782]);
        Assert.Equal(12, facts.JobLevels![28]);
        Assert.True(facts.Subscribed);
        Assert.False(WorldTravel.FactsOf(state, now.AddDays(3)).Subscribed);
        Assert.False(WorldTravel.FactsOf(state with { SubscriptionEnd = null }, now).Subscribed);
    }
}
