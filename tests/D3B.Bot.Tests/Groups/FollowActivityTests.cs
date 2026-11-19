using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Groups;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

public sealed class FollowActivityTests
{
    private static ActivityRun Follow(TestPlay play, BotActor follower, CharacterKey master)
    {
        return follower.StartActivity(new FollowActivity(new FollowSettings { Master = master.ToId() }, play.Bots, play.Tables));
    }

    private static int[] Around(int cell)
    {
        return [.. Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Neighbour(cell, direction)).Where(neighbour => neighbour >= 0)];
    }

    private static string Doing(ActivityRun run)
    {
        return TestGroup.Say(run.Activity.Report?.Doing);
    }

    [Fact]
    public async Task AFollowerFightsItsFightsAndChoosesTheChallengesBonusItsMastersProfileChooses()
    {
        // Measured on the owner's test of 2026-11-18 (D446): each character's client sends its own choice as it joins, so
        // a follower sends its master's, the owner's choice; the client's while its master runs no profile.
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300);
        var follow = new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables);

        Assert.True(follow.FightsItsFights);
        Assert.Equal(ChallengeReward.Client, follow.ChallengeReward);

        var leading = master.StartActivity(new RewardActivity { Reward = ChallengeReward.Drop });

        Assert.Equal(ChallengeReward.Drop, follow.ChallengeReward);

        leading.Stop();
        await leading.Completion;

        Assert.Equal(ChallengeReward.Client, follow.ChallengeReward);
    }

    [Fact]
    public async Task AFollowerLeavesTheBonusToItsClientUnderAMasterWhoseAIDoesNotFightOrWhoFollows()
    {
        // D438: a profile whose AI does not fight sends no bonus, its choice hidden; and two followers naming each other,
        // before the directory refuses the second, must not ask each other for ever.
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);
        var follow = new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables);
        var harvesting = master.StartActivity(new RewardActivity { Reward = ChallengeReward.Drop, Fights = false });

        Assert.Equal(ChallengeReward.Client, follow.ChallengeReward);

        harvesting.Stop();
        await harvesting.Completion;

        var back = new FollowActivity(new FollowSettings { Master = TestGroup.Follower.ToId() }, play.Bots, play.Tables);
        var masterFollows = master.StartActivity(back);
        var followerFollows = follower.StartActivity(follow);

        Assert.Equal(ChallengeReward.Client, follow.ChallengeReward);
        Assert.Equal(ChallengeReward.Client, back.ChallengeReward);
        masterFollows.Stop();
        followerFollows.Stop();
    }

    [Fact]
    public async Task AFollowerRefusesAStrangersInvitationOnceAndAcceptsItsMasters()
    {
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        connection.Server(new PartyInvitationEvent { PartyId = TestGroup.OtherPartyId, FromId = TestGroup.StrangerId, FromName = "Stranger", ToId = TestGroup.FollowerId, Type = PartyType.Classical });

        Assert.Equal(new PartyInvitationRefuseRequest { PartyId = TestGroup.OtherPartyId }, await TestGroup.SentAsync(play, connection, 1));

        connection.Server(new PartyInvitationEvent { PartyId = TestGroup.PartyId, FromId = TestGroup.MasterId, FromName = "Master", ToId = TestGroup.FollowerId, Type = PartyType.Classical });
        var invited = play.Time.GetUtcNow();

        // The owner's choice (D437): no sooner than the owner's own fastest answer in 10a's captures.
        Assert.Equal(new PartyInvitationAcceptRequest { PartyId = TestGroup.PartyId }, await TestGroup.SentAsync(play, connection, 2));
        Assert.True(play.Time.GetUtcNow() - invited >= TimeSpan.FromSeconds(1.3));

        // Measured (D437): the client turns both options on as the party is joined; the follower turns them off after it.
        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));
        var joined = play.Time.GetUtcNow();

        Assert.IsType<PartyAutoOptionOneOffRequest>(await TestGroup.SentAsync(play, connection, 3));
        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 4));
        Assert.True(play.Time.GetUtcNow() - joined >= BotDelays.Measured.Display);

        // Once per party: the stranger's invitation, refused, is not refused again.
        await TestGroup.SettleAsync(follower, master);
        play.Time.Advance(TimeSpan.FromSeconds(10));
        await TestGroup.SettleAsync(follower, master);

        Assert.Equal(4, connection.Sent.Count);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerRefusesAnInvitationOnlyOnceThoughItStaysInItsState()
    {
        await using var play = new TestPlay();
        await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");
        connection.Server(new PartyInvitationEvent { PartyId = TestGroup.OtherPartyId, FromId = TestGroup.StrangerId, FromName = "Stranger", ToId = TestGroup.FollowerId, Type = PartyType.Classical });

        Assert.Equal(new PartyInvitationRefuseRequest { PartyId = TestGroup.OtherPartyId }, await TestGroup.SentAsync(play, connection, 1));

        // D3B's refusal is no message its bot sees (D62): the invitation stays, step after step, and is not refused again.
        var steps = run.Steps;

        await TestGroup.UntilAsync(play, () => run.Steps >= steps + 3, "three steps more");

        Assert.NotNull(follower.State.Invitation);
        Assert.Single(connection.Sent);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerTurnsTheOptionsOffAgainOnceConnectedAgainInTheSameParty()
    {
        await using var play = new TestPlay();
        await TestGroup.MasterAsync(play, 300);
        var (first, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        first.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, first, 2));

        first.Close();

        var (connection, _) = await TestGroup.FollowerAsync(play, 301);

        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        Assert.IsType<PartyAutoOptionOneOffRequest>(await TestGroup.SentAsync(play, connection, 1));
        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 2));
        run.Stop();
    }

    [Fact]
    public async Task AFollowerRefusedItsHavenBagWaitsBeforeAskingAgain()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        var bag = TestPlay.Information(324, TestPlay.HavenBagMapId, TestGroup.MasterId);

        bag.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestGroup.MasterId } };
        masterConnection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        masterConnection.Server(bag);
        await master.ProcessedAsync();

        // The server answers nothing, as on a map that refuses the haven bag: the follower says so, and asks again only later.
        Assert.IsType<HavenBagEnterRequest>(await TestGroup.SentAsync(play, connection, 1));
        await TestGroup.UntilAsync(play, () => Doing(run) == "Finds no way to Master, tries again", "stuck");

        Assert.Single(connection.Sent);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerLeavesAPartyItsMasterDoesNotLead()
    {
        await using var play = new TestPlay();
        await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);

        connection.Server(TestGroup.Party(TestGroup.OtherPartyId, TestGroup.StrangerId, TestGroup.StrangerId, TestGroup.FollowerId));
        await follower.ProcessedAsync();

        var run = Follow(play, follower, TestGroup.Master);

        Assert.Equal(new PartyLeaveRequest { PartyId = TestGroup.OtherPartyId }, await TestGroup.SentAsync(play, connection, 1));
        run.Stop();
    }

    [Fact]
    public async Task AFollowerStaysWhereItStandsAsItsMasterWalksToAGroupOfMonsters()
    {
        // The owner's ask (D449): on its master's map, it joins the fight from where it stands, without walking beside it.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        var group = new MonsterGroup { Main = new GroupMonster { MonsterId = 489, Grade = 1, Level = 20 } };

        masterConnection.Server(new MapActorShownEvent { Actors = { new Actor { Id = -3, Disposition = new ActorDisposition { CellId = TestPlay.ActionCell }, Information = new ActorInformation { Roleplay = new RoleplayActor { MonsterGroup = new MonsterGroupActor { Monsters = group } } } } } });
        await master.ProcessedAsync();

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 300, TestPlay.ActionCell, out var path));
        masterConnection.Server(new MapMovementEvent { ActorId = TestGroup.MasterId, Cells = { path.Cells.ToArray() } });
        await TestGroup.SettleAsync(follower, master);
        play.Time.Advance(TimeSpan.FromSeconds(5));
        await TestGroup.SettleAsync(follower, master);

        Assert.Empty(connection.Sent);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerStaysWhereItStandsAsItsMasterWalksToADoor()
    {
        // Measured (D453): beside its master's door, a walk more onto the door's cell went unconfirmed, and the server held it.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 433);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        var step = play.StepTo(master, TestPlay.InsideMapId);

        Assert.Equal(WorldTransitionType.Interactive, step.Transition.Type);
        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 300, step.Transition.CellId, out var path));
        masterConnection.Server(new MapMovementEvent { ActorId = TestGroup.MasterId, Cells = { path.Cells.ToArray() } });
        await TestGroup.SettleAsync(follower, master);
        play.Time.Advance(TimeSpan.FromSeconds(5));
        await TestGroup.SettleAsync(follower, master);

        Assert.Empty(connection.Sent);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerThatWalkedWithItsMasterToItsExitChangesMapsAtOnce()
    {
        // The owner's choice (D447): standing on its master's exit, it goes as its master goes, not once its master is placed.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        var exit = play.StepTo(master, TestPlay.NorthMapId).Transition.CellId;

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 300, exit, out var path));
        masterConnection.Server(new MapMovementEvent { ActorId = TestGroup.MasterId, Cells = { path.Cells.ToArray() } });

        // An exit of its own, which leaves the map the way its master's does (D451).
        var walked = await TestGroup.WalkedAsync(play, connection, TestGroup.FollowerId, 1);
        var cells = play.Maps.Get(TestPlay.MapId).Cells;

        Assert.NotEqual(exit, walked);
        Assert.NotEqual(0, cells.Span[walked].MapChangeData & cells.Span[exit].MapChangeData);

        // The master changes maps; the server names its new map, and has not placed it yet.
        masterConnection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        await master.ProcessedAsync();

        // From its own exit at once, with no walk more (D451), its master not placed yet.
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestGroup.SentAsync(play, connection, 2));
        Assert.Null(master.State.Cell);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerGoesToTheMapItsMasterWentTo()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        // North, by the scroll the master took: a walk to the top edge, then the neighbour.
        await TestGroup.WalkedAsync(play, connection, TestGroup.FollowerId, 1);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestGroup.SentAsync(play, connection, 2));
        Assert.Equal("Goes to Master", Doing(run));

        TestGroup.Arrive(connection, TestGroup.FollowerId, TestPlay.NorthMapId, 530);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following again");
        run.Stop();
    }

    [Fact]
    public async Task AFollowerLeavesOnceItsMasterIsPlacedOnItsNewMap()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        // Measured in game (D437): leaving as the new map is named, the follower walked where its client still showed its master.
        masterConnection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        await master.ProcessedAsync();
        play.Time.Advance(TimeSpan.FromSeconds(10));
        await TestGroup.SettleAsync(follower, master);

        Assert.Empty(connection.Sent);

        masterConnection.Server(TestPlay.Information(500, TestPlay.NorthMapId, TestGroup.MasterId));

        Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, 1));
        run.Stop();
    }

    [Fact]
    public async Task AFollowerWhoseWalkTheClientDidNotFollowTriesTheSameWayAgain()
    {
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        // The first walk to the edge goes unanswered, as measured in game (D437): the change fails.
        Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, 1));

        // The next steps take the scroll north again, not the map action round it a journey would leave the scroll for.
        for (var count = 2; ; count++)
        {
            if (await TestGroup.SentAsync(play, connection, count) is MapChangeRequest change)
            {
                Assert.Equal((int)TestPlay.NorthMapId, change.MapId);

                break;
            }

            if (connection.Sent[count - 1] is MapMovementRequest)
                await TestGroup.WalkedAsync(play, connection, TestGroup.FollowerId, count);
        }

        // One change a step, none a journey's: a journey leaves the way that failed out and goes round where the world allows (D74).
        Assert.Empty(follower.Journal.All().OfType<TravelEntry>());
        Assert.Contains(follower.Journal.All().OfType<MapChangeEntry>(), entry => entry.Outcome is MapChangeOutcome.NotWalked);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerNeverGoesRoundWhileItsMasterIsOneMapAway()
    {
        // Measured on the owner's runs of 2026-11-19 (D456): after three changes failed, a follower's journey to the map its
        // master had reached in one change went round by nine; the way had not failed, the follower had.
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        // Each walk to the edge refused where the follower stands: six changes failed in a row.
        for (var count = 1; count <= 6; count++)
        {
            var walk = Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, count));
            var stood = MapGeometry.PointOf(walk.KeyMovements[0] & 0xFFF);

            connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });
        }

        await TestGroup.UntilAsync(play, () => Doing(run) == "Finds no way to Master, tries again", "stuck");
        Assert.Empty(follower.Journal.All().OfType<TravelEntry>());
        run.Stop();
    }

    [Fact]
    public async Task AFollowerRefusedItsMastersWayFromAnExitWalksBeforeAskingAgain()
    {
        // Measured on the owner's group runs of 2026-11-19 (D462): on its master's exit after a fight, a follower asked for the
        // neighbour every 8.5 s for thirty minutes, each refused naming its cell, its group waiting, until the server closed every
        // connection of the group for inactivity.
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 20);
        var run = Follow(play, follower, TestGroup.Master);
        var stood = MapGeometry.PointOf(20);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        Assert.IsType<MapChangeRequest>(await TestGroup.SentAsync(play, connection, 1));
        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });

        var walk = Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, 2));

        Assert.Equal(20, walk.KeyMovements[0] & 0xFFF);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerWhoseChangesFailFromWhereItStandsStepsAsideBeforeTheNext()
    {
        // The same way failing again and again from one cell (D456), whatever the server answers or not: a walk shown lets any
        // hold go and has the client draw its character where the server has it (D462).
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 20);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        for (var count = 1; count <= 3; count++)
            Assert.IsType<MapChangeRequest>(await TestGroup.SentAsync(play, connection, count));

        var walk = Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, 4));

        Assert.Equal(20, walk.KeyMovements[0] & 0xFFF);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerGoesWithoutWaitingForTheOneBeforeIt()
    {
        // The owner's choice (D447): in real time, whatever the others' order.
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, 300);
        var (_, first) = await TestGroup.SecondAsync(play, 302, name: "Abel");
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, name: "Zed");

        using var membership = play.Bots.Groups.Follow(first, TestGroup.Master, out _);
        var run = Follow(play, follower, TestGroup.Master);

        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, 1));
        run.Stop();
    }

    [Fact]
    public async Task AFollowerWalksWhereItsMasterWalksAsItWalksAndOnceAWalk()
    {
        // The owner's choice (D447): the follower copies each of its master's walks at once, to the cell the master walks to.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        // A cell that leads nowhere: no door, no map action, no edge (D451, D453).
        const int Plain = 344;

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 300, Plain, out var path));
        masterConnection.Server(new MapMovementEvent { ActorId = TestGroup.MasterId, Cells = { path.Cells.ToArray() } });

        var walk = Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, 1));
        var beside = walk.KeyMovements[^1] & 0xFFF;

        // Beside its master's cell, not on it (D448): the clients confirmed none of four walks to one cell.
        Assert.Contains(beside, Around(Plain));
        Assert.True(master.State.IsWalking);
        Assert.Contains(play.Log.GetSnapshot(), record => record.Message.Contains("walks with its master", StringComparison.Ordinal));

        // Walked and confirmed, the follower stands where its master goes, and copies that walk no more.
        var cells = CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 301, beside, out var own) ? own.Cells.ToArray() : [];

        connection.Server(new MapMovementEvent { ActorId = TestGroup.FollowerId, Cells = { cells } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        await TestGroup.SettleAsync(follower, master);
        play.Time.Advance(TimeSpan.FromSeconds(10));
        await TestGroup.SettleAsync(follower, master);

        Assert.Single(connection.Sent);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerWaitsWhereItStandsWhileItsMasterIsDisconnected()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300, TestPlay.NorthMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);

        masterConnection.Close();
        await TestGroup.UntilAsync(play, () => !master.IsConnected, "the master disconnected");

        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Waits for Master, disconnected", "waiting for the master");
        play.Time.Advance(TimeSpan.FromSeconds(30));
        await TestGroup.SettleAsync(follower);

        Assert.Empty(connection.Sent);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerEntersItsOwnHavenBagAsItsMasterDid()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following");

        var bag = TestPlay.Information(324, TestPlay.HavenBagMapId, TestGroup.MasterId);

        bag.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestGroup.MasterId } };
        masterConnection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        masterConnection.Server(bag);
        await master.ProcessedAsync();

        Assert.Equal(new HavenBagEnterRequest { OwnerId = TestGroup.FollowerId }, await TestGroup.SentAsync(play, connection, 1));
        run.Stop();
    }

    [Fact]
    public async Task ACharacterNamingItselfAsItsMasterFails()
    {
        await using var play = new TestPlay();
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Follower);

        Assert.Equal(ActivityOutcome.Failed, await run.Completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal("A character cannot follow itself.", TestGroup.Say(Assert.IsType<ActivityException>(run.Failure).Reason));
    }

    [Fact]
    public async Task AFollowerLeavesItsMastersGroupOnceItsActivityIsOver()
    {
        await using var play = new TestPlay();
        await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);
        var run = Follow(play, follower, TestGroup.Master);

        await TestGroup.UntilAsync(play, () => play.Bots.Groups.FollowersOf(TestGroup.Master).Count is 1, "in the group");
        run.Stop();
        await run.Completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Empty(play.Bots.Groups.FollowersOf(TestGroup.Master));
    }
}
