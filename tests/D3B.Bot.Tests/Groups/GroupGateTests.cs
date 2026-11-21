using D3B.Bot.Groups;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Groups;

public sealed class GroupGateTests
{
    // An action the master waits for its followers before: its walks and map changes no longer (D447).
    private static readonly AttackMonsterRequest _act = new() { MonsterGroupId = -1 };

    private static Task<bool> Act(BotActor bot, IMessage request)
    {
        return bot.RunAsync(async (context, token) =>
        {
            await context.SendAsync(request, token);

            return true;
        }, TestContext.Current.CancellationToken);
    }

    private static PartyInvitationRequest Invitation(string name)
    {
        return new PartyInvitationRequest { Type = PartyType.Classical, Target = new PlayerSearch { ByName = new PlayerSearch.Types.ByName { Name = name } } };
    }

    // The master's party holds the follower, as the server tells both once the follower accepted.
    private static void Joined(TestConnection master, TestConnection follower)
    {
        master.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId));
        master.Server(new PartyNewMemberEvent { PartyId = TestGroup.PartyId, Member = new Character { Id = TestGroup.FollowerId } });
        follower.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));
    }

    [Fact]
    public async Task AMasterTheGameFollowsAsksAFollowerWithItThatDoesNotFollowToFollowItEveryThirtySeconds()
    {
        // The owner's report (D476): the leader's "follow me" off, its members stop; on again, they follow again.
        await using var play = new TestPlay();
        var (masterConnection, master, followerConnection, follower) = await TestGroup.FollowedByTheGameAsync(play, 300, 301);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);

        followerConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));
        await follower.ProcessedAsync();
        await Act(master, _act);

        Assert.Equal(new PartyFollowMeRequest { Follow = true }, masterConnection.Sent[0]);
        Assert.Equal(_act, masterConnection.Sent[1]);

        await Act(master, _act);

        Assert.Equal(3, masterConnection.Sent.Count);

        play.Time.Advance(GroupGate.CallAgainAfter);
        await Act(master, _act);

        Assert.Equal(new PartyFollowMeRequest { Follow = true }, masterConnection.Sent[3]);

        // Its follower following, the master asks no more.
        followerConnection.Response(new PartyFollowLeaderResponse());
        await follower.ProcessedAsync();
        play.Time.Advance(GroupGate.CallAgainAfter);
        await Act(master, _act);

        Assert.Equal(_act, masterConnection.Sent[^1]);
        Assert.Equal(6, masterConnection.Sent.Count);
    }

    [Fact]
    public async Task AMasterNobodyFollowsActsAtOnce()
    {
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, 300);

        await Act(master, _act);

        Assert.Equal(_act, Assert.Single(connection.Sent));
        Assert.Empty(master.WaitingFor);
    }

    [Fact]
    public async Task AMasterInvitesItsFollowerAndWaitsForItOnItsMapAndInItsPartyBeforeActing()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var acting = Act(master, _act);

        Assert.Equal(Invitation("Follower"), await TestGroup.SentAsync(play, masterConnection, 1));
        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");
        Assert.Equal(new MissingMember(TestGroup.Follower, "Follower", MemberAbsence.Away), Assert.Single(master.WaitingFor));
        Assert.Equal("Waits for Follower, on its way", TestGroup.Say(GroupTexts.Waiting(master.WaitingFor)));

        // In the party, still away: the master waits.
        Joined(masterConnection, followerConnection);
        await TestGroup.SettleAsync(master, follower);

        Assert.False(acting.IsCompleted);
        Assert.Single(masterConnection.Sent);

        TestGroup.Arrive(followerConnection, TestGroup.FollowerId, TestPlay.MapId, 301);
        await acting;

        Assert.Equal(_act, masterConnection.Sent[^1]);
        Assert.Empty(master.WaitingFor);
    }

    [Fact]
    public async Task AMasterGoesOnWithoutAFollowerMissingFiveMinutesAndWaitsForItAgainOnceItIsBack()
    {
        // Measured on the owner's captures of 2026-11-19 (D464, D489): a master waited for a follower whose map change the
        // server refused about 210 times, until the server closed every connection of the group 30 minutes on.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);

        Joined(masterConnection, followerConnection);
        await TestGroup.SettleAsync(master, follower);

        var acting = Act(master, _act);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");
        play.Time.Advance(GroupGate.LeaveBehindAfter - TimeSpan.FromSeconds(1));
        await TestGroup.SettleAsync(master, follower);

        Assert.False(acting.IsCompleted);

        play.Time.Advance(TimeSpan.FromSeconds(1));
        await acting;

        Assert.Equal(_act, masterConnection.Sent[^1]);
        Assert.Empty(master.WaitingFor);
        Assert.Contains(master.Journal.All(), entry => entry is NoteEntry note && note.Text.Contains("Follower", StringComparison.Ordinal));

        // Left behind, it holds no action more.
        await Act(master, _act);

        Assert.Equal(2, masterConnection.Sent.Count(request => request.Equals(_act)));

        // Back with its master as it walks, a request the gate does not hold, it is waited for again as it is away again,
        // even on the map it was left on.
        TestGroup.Arrive(followerConnection, TestGroup.FollowerId, TestPlay.MapId, 301);
        await TestGroup.SettleAsync(master, follower);
        await Act(master, new MapMovementRequest { MapId = TestPlay.MapId });
        TestGroup.Arrive(followerConnection, TestGroup.FollowerId, TestPlay.NorthMapId, 300);
        await TestGroup.SettleAsync(master, follower);
        acting = Act(master, _act);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting again");

        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AFollowerLeftBehindIsWaitedForAgainOnceItLeftTheMapItWasLeftOn()
    {
        // On its way again, such as a map behind its master walking on: it is no longer held where it was.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);

        Joined(masterConnection, followerConnection);
        await TestGroup.SettleAsync(master, follower);

        var acting = Act(master, _act);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");
        play.Time.Advance(GroupGate.LeaveBehindAfter);
        await acting;

        TestGroup.Arrive(followerConnection, TestGroup.FollowerId, TestPlay.EastMapId, 300);
        await TestGroup.SettleAsync(master, follower);
        acting = Act(master, _act);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting again");

        Assert.Equal(MemberAbsence.Away, Assert.Single(master.WaitingFor).Absence);
        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AMasterNeverGoesOnWithoutAFollowerInAFight()
    {
        // A fight ends by itself: the time a follower spends in one counts for nothing.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 301);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);

        Joined(masterConnection, followerConnection);
        followerConnection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await TestGroup.SettleAsync(master, follower);

        var acting = Act(master, _act);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");

        Assert.Equal(MemberAbsence.Fighting, Assert.Single(master.WaitingFor).Absence);

        play.Time.Advance(GroupGate.LeaveBehindAfter * 2);
        await TestGroup.SettleAsync(master, follower);

        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AFollowerOnTheMapButOutOfThePartyIsWaitedForAndInvitedAgainEveryTenSeconds()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var acting = Act(master, _act);

        await TestGroup.SentAsync(play, masterConnection, 1);

        // The clock moves by tenths of a second as the test waits: the first invitation went at most one before.
        var asked = play.Time.GetUtcNow();

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");

        Assert.Equal(MemberAbsence.OutOfParty, Assert.Single(master.WaitingFor).Absence);
        Assert.Equal("Waits for Follower to join the party", TestGroup.Say(GroupTexts.Waiting(master.WaitingFor)));

        Assert.Equal(Invitation("Follower"), await TestGroup.SentAsync(play, masterConnection, 2));
        Assert.True(play.Time.GetUtcNow() - asked >= GroupGate.InviteAgainAfter - TimeSpan.FromMilliseconds(200));
        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AMasterInvitesAgainAFollowerConnectedAgain()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (first, follower) = await TestGroup.FollowerAsync(play, 301);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var acting = Act(master, _act);

        await TestGroup.SentAsync(play, masterConnection, 1);
        var invited = play.Time.GetUtcNow();

        first.Close();
        await TestGroup.UntilAsync(play, () => master.WaitingFor is [{ Absence: MemberAbsence.Disconnected }], "the follower disconnected");

        // A disconnected follower is waited for, and not invited.
        Assert.Single(masterConnection.Sent);

        await TestGroup.FollowerAsync(play, 301);

        // At once, not as the first invitation's ten seconds pass.
        Assert.Equal(Invitation("Follower"), await TestGroup.SentAsync(play, masterConnection, 2));
        Assert.True(play.Time.GetUtcNow() - invited < GroupGate.InviteAgainAfter);
        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AMasterLeavesAPartyAnotherLeadsBeforeInviting()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);

        masterConnection.Server(TestGroup.Party(TestGroup.OtherPartyId, TestGroup.StrangerId, TestGroup.StrangerId, TestGroup.MasterId));
        await master.ProcessedAsync();

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var acting = Act(master, _act);

        Assert.Equal(new PartyLeaveRequest { PartyId = TestGroup.OtherPartyId }, await TestGroup.SentAsync(play, masterConnection, 1));

        masterConnection.Server(new PartyLeaveEvent { PartyId = TestGroup.OtherPartyId });

        Assert.Equal(Invitation("Follower"), await TestGroup.SentAsync(play, masterConnection, 2));
        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AMasterKeepsAPartyAFollowerLeadsWhileItHoldsEachOfItsFollowersAndActsInIt()
    {
        // The owner's report of 2026-11-20 (D471): the party formed by hand, a follower leading it, is kept, not formed again.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 301);
        var (secondConnection, second) = await TestGroup.SecondAsync(play, 302);
        var party = TestGroup.Party(TestGroup.OtherPartyId, TestGroup.FollowerId, TestGroup.FollowerId, TestGroup.MasterId, TestGroup.SecondId);

        masterConnection.Server(party);
        followerConnection.Server(party);
        secondConnection.Server(party);
        await TestGroup.SettleAsync(master, follower, second);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        using var secondMembership = play.Bots.Groups.Follow(second, TestGroup.Master, out _);

        await Act(master, new MapMovementRequest { MapId = TestPlay.MapId });
        await Act(master, _act);

        // A member of a party another leads, the master turns its own automatic options off, once: joining its leader's
        // fights would take it into a follower's.
        Assert.Equal(
            [new PartyAutoOptionOneOffRequest(), new PartyAutoOptionTwoOffRequest(), new MapMovementRequest { MapId = TestPlay.MapId }, _act],
            masterConnection.Sent);
    }

    [Fact]
    public async Task AMasterLeavesAPartyAnotherLeadsOnceAFollowerIsOutOfIt()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 301);
        var (_, second) = await TestGroup.SecondAsync(play, 302);
        var party = TestGroup.Party(TestGroup.OtherPartyId, TestGroup.FollowerId, TestGroup.FollowerId, TestGroup.MasterId);

        masterConnection.Server(party);
        followerConnection.Server(party);
        await TestGroup.SettleAsync(master, follower);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        using var secondMembership = play.Bots.Groups.Follow(second, TestGroup.Master, out _);
        var acting = Act(master, _act);

        // Invitations into a party another leads are not measured: the master leaves it and forms its own.
        Assert.Equal(new PartyLeaveRequest { PartyId = TestGroup.OtherPartyId }, await TestGroup.SentAsync(play, masterConnection, 1));
        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AMasterLeavesAgainAPartyItIsStillInTenSecondsAfter()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);

        masterConnection.Server(TestGroup.Party(TestGroup.OtherPartyId, TestGroup.StrangerId, TestGroup.StrangerId, TestGroup.MasterId));
        await master.ProcessedAsync();

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var acting = Act(master, _act);

        await TestGroup.SentAsync(play, masterConnection, 1);
        var left = play.Time.GetUtcNow();

        // The server answered nothing: the master, still in that party, asks again, and invites nobody into it.
        Assert.Equal(new PartyLeaveRequest { PartyId = TestGroup.OtherPartyId }, await TestGroup.SentAsync(play, masterConnection, 2));
        Assert.True(play.Time.GetUtcNow() - left >= GroupGate.InviteAgainAfter - TimeSpan.FromMilliseconds(200));
        Assert.False(acting.IsCompleted);
    }

    [Fact]
    public async Task AMasterWalksAndChangesMapsAtOnceWhileItsFollowersAreAway()
    {
        // The owner's choice (D447): the followers copy their master's walks as they come, so it waits for them only before an action.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var change = new MapChangeRequest { MapId = (int)TestPlay.NorthMapId };

        await Act(master, new MapMovementRequest { MapId = TestPlay.MapId });
        await Act(master, change);

        Assert.Equal(change, masterConnection.Sent[^1]);
        Assert.Empty(master.WaitingFor);
    }

    [Fact]
    public async Task AMasterInvitesItsFollowersAtItsFirstWalkAndWalksAtOnce()
    {
        // The owner's report from game (D457): the master walked six maps before it invited its followers, at its first door.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var walk = new MapMovementRequest { MapId = TestPlay.MapId };

        await Act(master, walk);

        Assert.Equal(Invitation("Follower"), masterConnection.Sent[0]);
        Assert.Equal(walk, masterConnection.Sent[^1]);
        Assert.Empty(master.WaitingFor);

        // Unanswered, the invitation goes again 10 s later, not at each walk.
        await Act(master, walk);
        Assert.Single(masterConnection.Sent, request => request is PartyInvitationRequest);

        play.Time.Advance(GroupGate.InviteAgainAfter);
        await Act(master, walk);
        Assert.Equal(2, masterConnection.Sent.Count(request => request is PartyInvitationRequest));
    }

    [Fact]
    public async Task AMasterSendsWhatStartsNoActionAtOnceAndActsOnceItsLastFollowerLeaves()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);
        var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _)!;
        var refused = new PartyInvitationRefuseRequest { PartyId = TestGroup.OtherPartyId };

        await Act(master, refused);

        // At once, its follower invited first (D457).
        Assert.Equal([Invitation("Follower"), refused], masterConnection.Sent);

        var acting = Act(master, _act);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");

        // The follower's activity over, nobody follows the master: it acts.
        membership.Dispose();
        await acting;

        Assert.Equal(_act, masterConnection.Sent[^1]);
        Assert.Empty(master.WaitingFor);
    }
}
