using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class ForcedMovesTests
{
    private static Task<MapChangeOutcome> Leave(BotActor bot, int direction, int? cell = null)
    {
        return bot.RunAsync((context, token) => context.LeaveAsync(direction, cell, token), TestContext.Current.CancellationToken);
    }

    private static Task<MapChangeOutcome> TakeDoor(BotActor bot, int cell)
    {
        return bot.RunAsync((context, token) => context.TakeDoorAsync(cell, token), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ASideIsLeftFromTheNearestCellOfItsEdgeAndTheNeighbourAskedFor()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = Leave(bot, ForcedMoves.North);

        Assert.Equal(27, await TestWalks.WalkedAsync(play, connection, 1));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(2));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 538);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(TestPlay.NorthMapId, bot.State.MapId);
        Assert.Equal(MapChangeKind.Scroll, Assert.Single(bot.Journal.All().OfType<MapChangeEntry>()).Kind);
    }

    [Fact]
    public async Task ACellOfTheEdgeIsLeftFromByItsOwnSide()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = Leave(bot, ForcedMoves.South, 20);

        // Cell 20 is on the top edge, whatever side was asked.
        Assert.Equal(20, await TestWalks.WalkedAsync(play, connection, 1));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(2));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task ACellInsideTheMapIsNoWayOutAndNothingIsSent()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(MapChangeOutcome.NoPath, await Leave(bot, ForcedMoves.North, 300));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ASideWithoutANeighbourIsNoWayOutAndNothingIsSent()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        // The test map has a neighbour to the north and the east only.
        Assert.Equal(MapChangeOutcome.NoPath, await Leave(bot, ForcedMoves.West));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterThatWalksIsBusyAndLeavesNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 405 } });
        await bot.ProcessedAsync();

        Assert.Equal(MapChangeOutcome.Busy, await Leave(bot, ForcedMoves.North));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADoorIsFoundByTheCellOfItsElementAndUsedFromNextToIt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = TakeDoor(bot, TestPlay.DoorCell);

        var cell = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(-1, MapGeometry.Direction(cell, TestPlay.DoorCell));
        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.DoorInstanceId, ElementId = TestPlay.DoorElementId }, await connection.SentAsync(2));

        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.DoorElementId, SkillId = 184 });
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(MapChangeKind.Door, Assert.Single(bot.Journal.All().OfType<MapChangeEntry>()).Kind);
    }

    [Fact]
    public async Task ADoorTheServerRefusesIsRefusedAndACellWithoutAnElementIsNoPath()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(287);
        var change = TakeDoor(bot, TestPlay.DoorCell);

        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.DoorElementId, SkillInstanceId = 184 });

        Assert.Equal(MapChangeOutcome.Refused, await change);
        Assert.Equal(MapChangeOutcome.NoPath, await TakeDoor(bot, 12));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task TheHavenBagIsEnteredByTheRequestNamingTheCharactersOwn()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var change = bot.RunAsync((context, token) => context.EnterHavenBagAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal(new HavenBagEnterRequest { OwnerId = TestPlay.CharacterId }, await TestWalks.SentAsync(play, connection, 1));

        var information = TestPlay.Information(324, TestPlay.HavenBagMapId);

        information.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestPlay.CharacterId } };
        connection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        connection.Server(information);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.True(bot.State.IsInHavenBag);
        Assert.Equal(MapChangeKind.HavenBag, Assert.Single(bot.Journal.All().OfType<MapChangeEntry>()).Kind);
    }

    [Fact]
    public async Task ACharacterAlreadyInTheHavenBagEntersNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var information = TestPlay.Information(324, TestPlay.HavenBagMapId);

        information.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestPlay.CharacterId } };
        connection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        connection.Server(information);
        await bot.ProcessedAsync();

        Assert.Equal(MapChangeOutcome.NoPath, await bot.RunAsync((context, token) => context.EnterHavenBagAsync(token), TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }
}
