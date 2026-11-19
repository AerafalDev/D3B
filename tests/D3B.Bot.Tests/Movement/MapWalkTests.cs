using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Groups;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Movement;

public sealed class MapWalkTests
{
    private static CellPath PathOn(TestPlay play, int from, int to)
    {
        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, from, to, out var path));

        return path;
    }

    private static async Task<T> AdvanceUntilDoneAsync<T>(FakeTimeProvider time, Task<T> task)
    {
        // The walk starts its delays as it goes: the clock moves until it is over, whenever each delay started.
        while (!task.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return await task;
    }

    [Fact]
    public void ARequestNamesThePathsTurningPointsAsTheDirectionTimes4096PlusTheCell()
    {
        // A path the server walked when D3B sent it (client.md).
        var request = MapWalk.Request(TestPlay.MapId, new CellPath([433, 432, 431, 416, 388]));

        Assert.Equal([(4 * 4096) + 433, (5 * 4096) + 431, (6 * 4096) + 416, (6 * 4096) + 388], request.KeyMovements);
        Assert.Equal(TestPlay.MapId, request.MapId);
    }

    [Fact]
    public async Task AWalkSendsThePathAndIsOverOnceTheServerAnswersTheClientsConfirmation()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var path = PathOn(play, 433, 388);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        Assert.Equal(MapWalk.Request(TestPlay.MapId, path), await connection.SentAsync(1));

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        await bot.ProcessedAsync();

        Assert.False(walk.IsCompleted);
        Assert.True(bot.State.IsWalking);

        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());

        Assert.Equal(WalkOutcome.Arrived, await walk);
        await bot.ProcessedAsync();
        Assert.Equal(388, bot.State.Cell);
        Assert.False(bot.IsActing);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AWalkEndingWhereThePreviousOneEndedGoesInTwoForTheClientToFollowIt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var previous = PathOn(play, 433, 388);

        // A walk the client followed to cell 388, then the character placed on 433 again, as on the next map of a journey.
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { previous.Cells.ToArray() } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        TestWalks.Arrive(connection, TestPlay.MapId, 433);
        await bot.ProcessedAsync();

        Assert.Equal(388, bot.State.LastWalkEnd);

        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);
        var before = previous.Cells.Span[^2];

        Assert.Equal(before, await TestWalks.WalkedAsync(play, connection, 1));
        Assert.Equal(MapWalk.Request(TestPlay.MapId, PathOn(play, before, 388)), await TestWalks.SentAsync(play, connection, 2));
        Assert.Equal(388, await TestWalks.WalkedAsync(play, connection, 2));
        Assert.Equal(WalkOutcome.Arrived, await AdvanceUntilDoneAsync(play.Time, walk));
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task AWalkOnANewMapWaitsUntilTheClientDrewIt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);
        await bot.ProcessedAsync();

        var walk = bot.WalkAsync(300, TestContext.Current.CancellationToken);

        play.Time.Advance(BotDelays.Measured.Display - TimeSpan.FromMilliseconds(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(connection.Sent);

        play.Time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsType<MapMovementRequest>(await connection.SentAsync(1));
        Assert.False(walk.IsCompleted);
    }

    [Fact]
    public async Task AnotherActorsWalkIsNoAnswer()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = 42, Cells = { 100, 101 } });
        connection.Response(new MapMovementConfirmResponse());

        Assert.Equal(WalkOutcome.NoAnswer, await AdvanceUntilDoneAsync(play.Time, walk));
    }

    [Fact]
    public async Task ARefusedWalkIsOverAtOnceTheCharacterOnTheCellTheServerNames()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var walk = bot.WalkAsync(300, TestContext.Current.CancellationToken);
        var stood = MapGeometry.PointOf(388);

        await connection.SentAsync(1);
        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });

        Assert.Equal(WalkOutcome.Refused, await walk);
        await bot.ProcessedAsync();
        Assert.Equal(388, bot.State.Cell);
    }

    [Fact]
    public async Task AWalkTheServerDoesNotAnswerIsOverAfterItsDelay()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);

        Assert.Equal(WalkOutcome.NoAnswer, await AdvanceUntilDoneAsync(play.Time, walk));
    }

    [Fact]
    public async Task AWalkNotConfirmedIsOverAfterItsDelayWithTheCharacterWhereTheServerHasIt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var path = PathOn(play, 433, 388);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.NotConfirmed, await AdvanceUntilDoneAsync(play.Time, walk));
        Assert.False(bot.State.IsWalking);
        Assert.Equal(388, bot.State.Cell);
    }

    [Fact]
    public void APathWithNoStepIsNoRequest()
    {
        // Measured once: the server refused a request naming no cell, then closed the game connection.
        Assert.Throws<ArgumentException>(() => MapWalk.Request(TestPlay.MapId, new CellPath([433])));
    }

    [Fact]
    public async Task AWalkTheServerStillHoldsIsLetGoByAStepAsideAndBack()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var path = PathOn(play, 433, 388);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.NotConfirmed, await AdvanceUntilDoneAsync(play.Time, walk));
        Assert.True(bot.State.IsWalkHeld);

        // Already on the cell, the bot still walks: from it to a neighbour, which the server takes, then back.
        var again = bot.WalkAsync(388, TestContext.Current.CancellationToken);
        var aside = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));

        Assert.Equal(388, aside.KeyMovements[0] & 0xFFF);
        Assert.NotEqual(-1, MapGeometry.Direction(388, aside.KeyMovements[^1] & 0xFFF));

        await TestWalks.WalkedAsync(play, connection, 2);

        Assert.Equal(388, await TestWalks.WalkedAsync(play, connection, 3));
        Assert.Equal(WalkOutcome.Arrived, await again);
        Assert.False(bot.State.IsWalkHeld);
        Assert.Equal(388, bot.State.Cell);
    }

    [Fact]
    public async Task ACellTheCharacterCannotReachSendsNothing()
    {
        await using var play = new TestPlay(388);
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(WalkOutcome.NoPath, await bot.WalkAsync(388, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterAlreadyThereHasArrived()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(WalkOutcome.Arrived, await bot.WalkAsync(433, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterWhosePlaceIsNotKnownOrThatWalksIsBusy()
    {
        await using var play = new TestPlay();
        var connection = play.Connect(TestPlay.Account);

        connection.Server(TestPlay.Chosen());
        Assert.True(play.Bots.TryGet(TestPlay.Key, out var bot));
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(388, TestContext.Current.CancellationToken));

        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(433));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 432 } });
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(388, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterInADialogIsBusy()
    {
        // The server refuses a walk during the banker's dialog and during the bank's exchange, measured.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(396, TestPlay.InsideMapId);

        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(368, TestContext.Current.CancellationToken));

        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage });
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(368, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ThePlayerGivingANewPathInterruptsTheWalkAndPausesTheBot()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var path = PathOn(play, 433, 388);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        connection.Client(new MapMovementCancelRequest { CellId = path.Cells.Span[1] });
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId, KeyMovements = { (2 * 4096) + path.Cells.Span[1], (2 * 4096) + path.Cells.Span[1] + 28 } });

        var interrupted = await Assert.ThrowsAsync<BotInterruptedException>(() => walk);

        Assert.Equal(Interruption.Player, interrupted.Reason);
        await bot.ProcessedAsync();
        Assert.True(bot.IsPaused);
        Assert.Equal(path.Cells.Span[1], bot.State.Cell);
    }

    [Fact]
    public async Task TheConnectionClosingInterruptsTheWalk()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Close();

        Assert.Equal(Interruption.Disconnected, (await Assert.ThrowsAsync<BotInterruptedException>(() => walk)).Reason);
    }

    [Fact]
    public async Task AWalkIsLoggedFromItsStartToItsEnd()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var path = PathOn(play, 433, 388);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        connection.Response(new MapMovementConfirmResponse());
        await walk;

        var messages = play.Log.GetSnapshot().Select(record => record.Message).ToList();

        Assert.Contains($"Bot {TestPlay.CharacterId}: walks from cell 433 to cell 388, {path.Steps} steps", messages);
        Assert.Contains($"Bot {TestPlay.CharacterId}: walk to cell 388 over, Arrived", messages);
    }

    [Fact]
    public async Task AWalkToACellAnotherBotWalksToGoesOnceThatWalkIsOver()
    {
        // Measured on the owner's runs of 2026-11-19 (D456): of walks to a cell another bot of the map walked to, the clients
        // followed 9 of 28; to a free cell, 201 of 207.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var (otherConnection, other) = await TestGroup.FollowerAsync(play, 301);
        var path = PathOn(play, 300, 344);
        var walk = bot.WalkAsync(344, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        await bot.ProcessedAsync();

        var otherWalk = other.WalkAsync(344, TestContext.Current.CancellationToken);

        play.Time.Advance(TimeSpan.FromSeconds(1));
        await TestGroup.SettleAsync(bot, other);
        Assert.Empty(otherConnection.Sent);

        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        Assert.Equal(WalkOutcome.Arrived, await walk);

        var request = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, otherConnection, 1));

        Assert.Equal(344, request.KeyMovements[^1] & 0xFFF);
        Assert.False(otherWalk.IsCompleted);
    }

    [Fact]
    public async Task AWalkToACellAnotherBotStandsOnGoesAtOnce()
    {
        // Walks to a cell another bot stood on were followed 3 times of 4 (D456), and a master would wait on its followers.
        await using var play = new TestPlay();
        var (_, bot) = await play.EnterAsync(344);
        var (otherConnection, other) = await TestGroup.FollowerAsync(play, 301);
        var walk = other.WalkAsync(344, TestContext.Current.CancellationToken);

        Assert.IsType<MapMovementRequest>(await otherConnection.SentAsync(1));
        Assert.Equal(344, bot.State.Cell);
        Assert.False(walk.IsCompleted);
    }

    [Fact]
    public async Task AWalkToACellAnotherBotClaimsGoesOnceThatWalkIsUnanswered()
    {
        // A bot whose walk the server leaves unanswered keeps the others waiting a while, not for good.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var (otherConnection, other) = await TestGroup.FollowerAsync(play, 301);
        var walk = bot.WalkAsync(344, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);

        var otherWalk = other.WalkAsync(344, TestContext.Current.CancellationToken);

        play.Time.Advance(TimeSpan.FromSeconds(2));
        await TestGroup.SettleAsync(bot, other);
        Assert.Empty(otherConnection.Sent);

        // Its walk unanswered, the first bot's claim goes with it.
        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, otherConnection, 1));
        Assert.Equal(WalkOutcome.NoAnswer, await walk);
        Assert.False(otherWalk.IsCompleted);
    }

    [Fact]
    public async Task AStepAsideGoesToANeighbourNoOtherBotStandsOn()
    {
        await using var play = new TestPlay();
        var cells = play.Maps.Get(TestPlay.MapId).Cells.Span;
        var neighbours = Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Neighbour(20, direction)).Where(cell => cell >= 0).ToArray();

        Assert.True(CellPathfinder.TryFindNearest(cells, 20, neighbours, out var nearest));

        var (_, other) = await TestGroup.FollowerAsync(play, nearest.End);
        var (connection, bot) = await play.EnterAsync(48);
        var walk = bot.WalkAsync(20, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { PathOn(play, 48, 20).Cells.ToArray() } });
        Assert.Equal(WalkOutcome.NotConfirmed, await AdvanceUntilDoneAsync(play.Time, walk));

        var aside = bot.RunAsync((context, token) => context.ReleaseHeldWalkAsync(token), TestContext.Current.CancellationToken);
        var request = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));

        Assert.Equal(20, request.KeyMovements[0] & 0xFFF);
        Assert.NotEqual(nearest.End, request.KeyMovements[^1] & 0xFFF);
        Assert.Equal(other.State.Cell, nearest.End);
        Assert.False(aside.IsCompleted);
    }

    [Fact]
    public async Task AStepAsideLeavesOutTheCellThePreviousWalkEndedOn()
    {
        // D464's case: a step onto a neighbour taken for arrived, then a refusal naming the cell it left; a step back onto that
        // neighbour is a walk ending where the previous one did, which the client does not follow (9g5).
        await using var play = new TestPlay();
        var cells = play.Maps.Get(TestPlay.MapId).Cells.Span;
        var neighbours = Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Neighbour(20, direction)).Where(cell => cell >= 0).ToArray();

        Assert.True(CellPathfinder.TryFindNearest(cells, 20, neighbours, out var nearest));

        var (connection, bot) = await play.EnterAsync(20);
        var stood = MapGeometry.PointOf(20);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 20, nearest.End } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });
        await bot.ProcessedAsync();

        Assert.True(bot.State.IsWalkHeld);
        Assert.Equal(nearest.End, bot.State.LastWalkEnd);

        var aside = bot.RunAsync((context, token) => context.StepAsideAsync(token), TestContext.Current.CancellationToken);
        var request = Assert.IsType<MapMovementRequest>(await connection.SentAsync(1));

        Assert.Equal(20, request.KeyMovements[0] & 0xFFF);
        Assert.NotEqual(nearest.End, request.KeyMovements[^1] & 0xFFF);
        Assert.False(aside.IsCompleted);
    }
}
