using D3B.Bot.Characters;
using D3B.Bot.Groups;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

public sealed class GroupChainTests
{
    private static CharacterState Placed(CharacterKey key, long mapId, int? cell = 300)
    {
        return new CharacterState { Key = key, Name = "", MapId = mapId, Cell = cell };
    }

    [Fact]
    public async Task TheChainGoesInTheMastersPartysOrderThenByName()
    {
        await using var play = new TestPlay();
        var (_, follower) = await TestGroup.FollowerAsync(play, 300, name: "Zed");
        var (_, second) = await TestGroup.SecondAsync(play, 301, name: "Abel");
        var master = Placed(TestGroup.Master, TestPlay.MapId);

        // Out of any party, by name: Abel first.
        Assert.Equal([second, follower], GroupChain.Order(master, [follower, second]));

        // In the master's party, its order first, whatever the names.
        var party = master with { Party = new CharacterParty(TestGroup.PartyId, TestGroup.MasterId, [TestGroup.MasterId, TestGroup.FollowerId]) };

        Assert.Equal([follower, second], GroupChain.Order(party, [second, follower]));

        // A party another leads gives no order.
        var others = master with { Party = new CharacterParty(TestGroup.PartyId, TestGroup.StrangerId, [TestGroup.StrangerId, TestGroup.FollowerId]) };

        Assert.Equal([second, follower], GroupChain.Order(others, [follower, second]));
    }

    [Fact]
    public void AMemberIsWithItsMasterPlacedOnItsMapOrEachInItsOwnHavenBag()
    {
        var master = Placed(TestGroup.Master, TestPlay.MapId);
        var follower = Placed(TestGroup.Follower, TestPlay.MapId);

        Assert.True(GroupChain.Together(master, follower));
        Assert.False(GroupChain.Together(master, follower with { MapId = TestPlay.NorthMapId }));

        // Named on the map but not yet placed on it, it is not there yet.
        Assert.False(GroupChain.Together(master, follower with { Cell = null }));

        var bagged = master with { MapId = TestPlay.HavenBagMapId, HavenBagOwnerId = TestGroup.MasterId };

        Assert.False(GroupChain.Together(bagged, follower));
        Assert.True(GroupChain.Together(bagged, follower with { MapId = TestPlay.HavenBagMapId + 1, HavenBagOwnerId = TestGroup.FollowerId }));

        // In another's haven bag, the master's own among them, no member is with it: each enters its own.
        Assert.False(GroupChain.Together(bagged, follower with { MapId = TestPlay.HavenBagMapId, HavenBagOwnerId = TestGroup.MasterId }));
        Assert.False(GroupChain.Together(master, follower with { HavenBagOwnerId = TestGroup.FollowerId }));
    }

    [Fact]
    public async Task AFollowerIsMissingDisconnectedPausedFightingAwayOrOutOfTheParty()
    {
        await using var play = new TestPlay();
        var (connection, follower) = await TestGroup.FollowerAsync(play, 300);
        var master = Placed(TestGroup.Master, TestPlay.MapId);
        var party = master with { Party = new CharacterParty(TestGroup.PartyId, TestGroup.MasterId, [TestGroup.MasterId, TestGroup.FollowerId]) };

        Assert.Null(GroupChain.AbsenceOf(party, follower));
        Assert.Equal(MemberAbsence.OutOfParty, GroupChain.AbsenceOf(master, follower));
        Assert.Equal(MemberAbsence.Away, GroupChain.AbsenceOf(party with { MapId = TestPlay.NorthMapId }, follower));

        var missing = Assert.Single(GroupChain.Missing(master, [follower]));

        Assert.Equal(new MissingMember(TestGroup.Follower, "Follower", MemberAbsence.OutOfParty), missing);

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await follower.ProcessedAsync();

        Assert.Equal(MemberAbsence.Fighting, GroupChain.AbsenceOf(party, follower));

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(TestPlay.Information(300, TestPlay.MapId, TestGroup.FollowerId));
        await follower.ProcessedAsync();

        Assert.Null(GroupChain.AbsenceOf(party, follower));

        follower.Pause();
        await follower.ProcessedAsync();

        Assert.Equal(MemberAbsence.Paused, GroupChain.AbsenceOf(party, follower));

        connection.Close();
        await TestGroup.UntilAsync(play, () => !follower.IsConnected, "disconnected");

        Assert.Equal(MemberAbsence.Disconnected, GroupChain.AbsenceOf(party, follower));
    }
}
