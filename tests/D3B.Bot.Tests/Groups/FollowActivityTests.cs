using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Groups;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

public sealed class FollowActivityTests
{
    private static ActivityRun Follow(TestPlay play, BotActor follower, CharacterKey master)
    {
        return follower.StartActivity(new FollowActivity(new FollowSettings { Master = master.ToId() }, play.Bots, play.Tables));
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
    public async Task AFollowerLetsTheOneBeforeItGoFirst()
    {
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, 300);
        var (firstConnection, first) = await TestGroup.SecondAsync(play, 302, name: "Abel");
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, name: "Zed");

        using var membership = play.Bots.Groups.Follow(first, TestGroup.Master, out _);
        var run = Follow(play, follower, TestGroup.Master);

        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Waits for Abel to go first", "behind");
        await TestGroup.SettleAsync(follower, first);
        Assert.Empty(connection.Sent);

        TestGroup.Arrive(firstConnection, TestGroup.SecondId, TestPlay.NorthMapId, 530);

        Assert.IsType<MapMovementRequest>(await TestGroup.SentAsync(play, connection, 1));
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
