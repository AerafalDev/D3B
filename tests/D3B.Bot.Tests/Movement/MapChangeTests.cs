using D3B.Bot.Movement;
using D3B.Engine.Criteria;
using D3B.Engine.Maps;
using D3B.Bot.Tests.Groups;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.World;
using D3B.Engine.World;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class MapChangeTests
{
    [Fact]
    public async Task AScrollWalksToTheNearestCellOfItsEdgeThenAsksForTheNeighbour()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        // From cell 433, east edge, row 30: straight north to row 1, two rows a step.
        Assert.Equal(27, await TestWalks.WalkedAsync(play, connection, 1));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(2));

        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        await bot.ProcessedAsync();
        Assert.False(change.IsCompleted);

        connection.Client(new MapInformationRequest { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(538, TestPlay.NorthMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        await bot.ProcessedAsync();
        Assert.Equal(TestPlay.NorthMapId, bot.State.MapId);
        Assert.Equal(538, bot.State.Cell);
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task AScrollLeavesByAnotherCellOfItsEdgeThanTheOneThePreviousWalkEndedOn()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        // The previous walk ended on cell 27, the edge's nearest from 433, as on the map before of a journey straight north.
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 461, 433 } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 27 } });
        await bot.ProcessedAsync();
        TestWalks.Arrive(connection, TestPlay.MapId, 433);
        await bot.ProcessedAsync();

        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var exit = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(27, exit);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(2));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 538);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task AWalkToTheEdgeThatSetsOffAMapActionAsksForNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(538, TestPlay.NorthMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AMapChangeIsOverOnceTheCharacterIsPlaced()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await change);
    }

    [Fact]
    public async Task ACharacterOnACellOfTheEdgeAsksAtOnce()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(1));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task ACharacterOnACellOfTheEdgeTheServerStillHoldsWalkingStepsAsideFirst()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(48);

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 48, 20, out var path));

        var walk = bot.WalkAsync(20, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        Assert.Equal(WalkOutcome.NotConfirmed, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));

        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var aside = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));

        Assert.Equal(20, aside.KeyMovements[0] & 0xFFF);

        await TestWalks.WalkedAsync(play, connection, 2);

        // From the cell it stepped to, the nearest of the edge's, walked to unless it is one.
        var request = await TestWalks.SentAsync(play, connection, 3);

        if (request is MapMovementRequest)
        {
            await TestWalks.WalkedAsync(play, connection, 3);
            request = await TestWalks.SentAsync(play, connection, 4);
        }

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, request);

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task ACharacterRefusedAMapChangeOnACellOfTheEdgeStepsAsideBeforeAskingAgain()
    {
        // Measured on the owner's group runs of 2026-11-19 (D462): a follower on a cell of the edge asked for the neighbour every
        // 8.5 s for thirty minutes, each refused, the server naming that cell; a walk it shows lets the hold go (D89).
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var first = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var stood = MapGeometry.PointOf(20);

        Assert.IsType<MapChangeRequest>(await connection.SentAsync(1));
        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });
        Assert.NotEqual(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, first));

        var again = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var aside = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));

        Assert.Equal(20, aside.KeyMovements[0] & 0xFFF);

        await TestWalks.WalkedAsync(play, connection, 2);

        var request = await TestWalks.SentAsync(play, connection, 3);

        if (request is MapMovementRequest)
        {
            await TestWalks.WalkedAsync(play, connection, 3);
            request = await TestWalks.SentAsync(play, connection, 4);
        }

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, request);

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, again));
    }

    [Fact]
    public async Task AMapActionWalksOntoItsCellAndTheServerNamesTheNewMapBeforeAnsweringTheConfirmation()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.EastMapId), TestContext.Current.CancellationToken);

        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId }));

        connection.Client(new MapInformationRequest { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        await bot.ProcessedAsync();
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Equal(253, bot.State.Cell);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ACharacterOnAMapActionsCellStepsOffThenBack()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(TestPlay.ActionCell);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.EastMapId), TestContext.Current.CancellationToken);

        var off = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(TestPlay.ActionCell, off);
        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 2, new MapCurrentEvent { MapId = TestPlay.EastMapId }));

        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task ACharacterTheServerSendsElsewhereIsElsewhere()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        TestWalks.Arrive(connection, TestPlay.EastMapId, 253);

        Assert.Equal(MapChangeOutcome.Elsewhere, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task AMapChangeTheServerDoesNotAnswerIsOverAfterItsDelay()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);

        Assert.Equal(MapChangeOutcome.NoAnswer, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(TestPlay.MapId, bot.State.MapId);
    }

    [Fact]
    public async Task ANewMapWhoseInformationDoesNotComeLeavesTheCharacterNotPlaced()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });

        Assert.Equal(MapChangeOutcome.NotPlaced, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Null(bot.State.Cell);
    }

    [Fact]
    public async Task AnInformationOfTheOldMapPlacesNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(TestPlay.Information(20));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        await bot.ProcessedAsync();

        Assert.False(change.IsCompleted);

        connection.Server(TestPlay.Information(552, TestPlay.NorthMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task AStepThatDoesNotLeaveWhereTheCharacterStandsSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var south = new WorldTransition(WorldTransitionType.Scroll, 2, 539, -1, TestPlay.MapId, -1, -1);

        Assert.Equal(MapChangeOutcome.NoPath, await bot.ChangeMapAsync(new WorldStep(0, 1, 0, south), TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnEdgeTheCharacterCannotReachSendsNothing()
    {
        // Rows 0 to 3 blocked: no cell of the north edge can be walked on.
        await using var play = new TestPlay([.. Enumerable.Range(0, 56)]);
        var (connection, bot) = await play.EnterAsync(433);
        var step = new WorldStep(1, 0, 1, play.World.Graph.Transitions.Span[1]);

        Assert.Equal(MapChangeOutcome.NoPath, await bot.ChangeMapAsync(step, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterThatWalksIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var step = play.StepTo(bot, TestPlay.NorthMapId);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 405 } });
        await bot.ProcessedAsync();

        Assert.Equal(MapChangeOutcome.Busy, await bot.ChangeMapAsync(step, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADoorIsUsedFromNextToItsElementThenTheServerNamesTheNewMap()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.InsideMapId), TestContext.Current.CancellationToken);

        var cell = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(-1, MapGeometry.Direction(cell, TestPlay.DoorCell));
        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.DoorInstanceId, ElementId = TestPlay.DoorElementId }, await connection.SentAsync(2));

        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.DoorElementId, SkillId = 184 });
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(TestPlay.InsideMapId, bot.State.MapId);
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task ADoorTheServerRefusesIsRefused()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(287);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.InsideMapId), TestContext.Current.CancellationToken);

        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.DoorElementId, SkillInstanceId = 184 });

        Assert.Equal(MapChangeOutcome.Refused, await change);
        Assert.Equal(TestPlay.MapId, bot.State.MapId);
    }

    [Fact]
    public async Task ADoorWhoseSkillTheServerDoesNotOfferSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var step = play.StepTo(bot, TestPlay.InsideMapId);

        connection.Server(new InteractiveElementUpdatedEvent { Element = new InteractiveElement { ElementId = TestPlay.DoorElementId, InteractiveId = 316 } });
        await bot.ProcessedAsync();

        Assert.Equal(MapChangeOutcome.NoPath, await bot.ChangeMapAsync(step, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TwoBotsLeavingByOneScrollAtOnceWalkToTwoCellsOfItsEdge()
    {
        // Measured on the owner's runs of 2026-11-19 (D456): three followers on one cell walked to the edge's same nearest
        // cell at once, and the clients of two did not follow their walks.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var (otherConnection, other) = await TestGroup.FollowerAsync(play, 433);

        _ = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        _ = other.ChangeMapAsync(play.StepTo(other, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        var walk = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1));
        var otherWalk = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, otherConnection, 1));

        Assert.NotEqual(walk.KeyMovements[^1] & 0xFFF, otherWalk.KeyMovements[^1] & 0xFFF);
    }

    [Fact]
    public async Task ACharacterTheServerStillHoldsUsesNoDoorBeforeAWalkOfItsIsConfirmed()
    {
        // Measured on the owner's runs of 2026-11-19 (D456): held, then refused a walk, a follower used the bank's door from
        // the cell the refusal named 25 times, each refused.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 433, 287, out var path));

        var walk = bot.WalkAsync(287, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        Assert.Equal(WalkOutcome.NotConfirmed, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));

        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.InsideMapId), TestContext.Current.CancellationToken);
        var stood = MapGeometry.PointOf(287);

        Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));
        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });

        Assert.Equal(MapChangeOutcome.NotWalked, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.DoesNotContain(connection.Sent, request => request is InteractiveUseRequest);
        Assert.True(bot.State.IsWalkHeld);
        Assert.Equal(287, bot.State.Cell);
    }

    [Fact]
    public async Task ABotTakesADoorsCellInTurnOnceTheBotOnItLeftTheMap()
    {
        // Measured on the owner's runs of 2026-11-19 (D456): three followers walked to the bank door's cells at once, and the
        // last was held by the server for minutes.
        await using var play = new TestPlay();
        var first = await play.EnterAsync(433);
        var step = play.StepTo(first.Bot, TestPlay.InsideMapId);
        var rules = new WorldPathRules { Facts = new CriterionFacts { Subscribed = true } };
        var exits = new WorldPathfinder(play.World.Graph, play.World.Atlas).ExitCells(play.Maps.Get(TestPlay.MapId), step, rules);
        var bots = new List<(TestConnection Connection, BotActor Bot, long Id)> { (first.Connection, first.Bot, TestPlay.CharacterId) };

        for (var count = 1; count <= exits.Count; count++)
        {
            var id = 100_000L + count;
            var (connection, bot) = await play.EnterAsync(433, TestPlay.MapId, new GameAccount(100 + count, $"bot{count}", "tag"), id, $"Bot{count}");

            bots.Add((connection, bot, id));
        }

        var doors = new HashSet<int>();

        // Every door's cell its own: each walked to, the door asked for, the new map not yet named.
        foreach (var (connection, bot, id) in bots.Take(exits.Count))
        {
            _ = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.InsideMapId), TestContext.Current.CancellationToken);
            doors.Add(await TestGroup.WalkedAsync(play, connection, id, 1));
            Assert.IsType<InteractiveUseRequest>(await TestGroup.SentAsync(play, connection, 2));
        }

        Assert.Equal(exits.Count, doors.Count);

        var (lastConnection, last, _) = bots[^1];
        var change = last.ChangeMapAsync(play.StepTo(last, TestPlay.InsideMapId), TestContext.Current.CancellationToken);

        play.Time.Advance(TimeSpan.FromSeconds(2));
        await TestGroup.SettleAsync([.. bots.Select(bot => bot.Bot)]);
        Assert.Empty(lastConnection.Sent);

        // The first goes in: its cell is the next one's.
        bots[0].Connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.DoorElementId, SkillId = 184 });
        TestWalks.Arrive(bots[0].Connection, TestPlay.InsideMapId, 396);

        var walk = Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, lastConnection, 1));

        Assert.Contains(walk.KeyMovements[^1] & 0xFFF, doors);
        Assert.False(change.IsCompleted);
    }

    [Fact]
    public async Task ACharacterTheServerStillHoldsAsksForNoHavenBag()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 433, 388, out var path));

        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        Assert.Equal(WalkOutcome.NotConfirmed, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));

        var enter = bot.RunAsync((context, token) => context.EnterHavenBagAsync(token), TestContext.Current.CancellationToken);
        var stood = MapGeometry.PointOf(388);

        Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));
        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });

        Assert.Equal(MapChangeOutcome.NotWalked, await TestWalks.AdvanceUntilDoneAsync(play.Time, enter));
        Assert.DoesNotContain(connection.Sent, request => request is HavenBagEnterRequest);
    }
}
