using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Groups;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

public sealed class FollowActivityTests
{
    private static ActivityRun Follow(TestPlay play, BotActor follower, CharacterKey master)
    {
        return follower.StartActivity(new FollowActivity(new FollowSettings { Master = master.ToId() }, play.Bots));
    }

    private static string Doing(ActivityRun run)
    {
        return TestGroup.Say(run.Activity.Report?.Doing);
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

        Assert.Equal(new PartyInvitationAcceptRequest { PartyId = TestGroup.PartyId }, await TestGroup.SentAsync(play, connection, 2));

        // Measured (D436): the client turns both options on as the party is joined; the follower turns them off after it.
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
