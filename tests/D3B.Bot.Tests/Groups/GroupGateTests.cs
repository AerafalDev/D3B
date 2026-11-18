using D3B.Bot.Groups;
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
    public async Task AMasterSendsWhatStartsNoActionAtOnceAndActsOnceItsLastFollowerLeaves()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);
        var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _)!;
        var refused = new PartyInvitationRefuseRequest { PartyId = TestGroup.OtherPartyId };

        await Act(master, refused);

        Assert.Equal(refused, Assert.Single(masterConnection.Sent));

        var acting = Act(master, _act);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");

        // The follower's activity over, nobody follows the master: it acts.
        membership.Dispose();
        await acting;

        Assert.Equal(_act, masterConnection.Sent[^1]);
        Assert.Empty(master.WaitingFor);
    }
}
