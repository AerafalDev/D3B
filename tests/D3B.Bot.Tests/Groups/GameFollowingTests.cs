using D3B.Bot.Characters;
using D3B.Bot.Groups;

namespace D3B.Bot.Tests.Groups;

/// <summary>The group the game follows (D476): every account subscribed, every follower of level 10 or more, the master leading its party.</summary>
public sealed class GameFollowingTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 20, 6, 0, 0, TimeSpan.Zero);

    private static CharacterState Master(long leaderId = TestGroup.MasterId, DateTimeOffset? subscription = null)
    {
        return new CharacterState
        {
            Key = TestGroup.Master,
            Name = "Master",
            Level = 200,
            SubscriptionEnd = subscription ?? _now.AddDays(1),
            Party = new CharacterParty(TestGroup.PartyId, leaderId, [TestGroup.MasterId, TestGroup.FollowerId]),
        };
    }

    private static CharacterState Follower(int? level = 10, DateTimeOffset? subscription = null)
    {
        return new CharacterState { Key = TestGroup.Follower, Name = "Follower", Level = level, SubscriptionEnd = subscription ?? _now.AddDays(1) };
    }

    [Fact]
    public void ASubscribedGroupOfFollowersOfLevelTenLedByItsMasterFollowsByTheGame()
    {
        Assert.True(GameFollowing.Applies(Master(), [Follower()], _now));
    }

    [Fact]
    public void AFollowerUnderLevelTenOrOfAnUnknownLevelLeavesTheGroupToTheBot()
    {
        Assert.False(GameFollowing.Applies(Master(), [Follower(), Follower(9)], _now));
        Assert.False(GameFollowing.Applies(Master(), [Follower(null)], _now));
    }

    [Fact]
    public void AnAccountNotSubscribedLeavesTheGroupToTheBot()
    {
        Assert.False(GameFollowing.Applies(Master(subscription: _now), [Follower()], _now));
        Assert.False(GameFollowing.Applies(Master(), [Follower(subscription: _now.AddSeconds(-1))], _now));
    }

    [Fact]
    public void APartyAnotherLeadsOrNoFollowerLeavesTheGroupToTheBot()
    {
        // The game follows the party's leader, whoever it is (D471): a party formed by hand that a follower leads.
        Assert.False(GameFollowing.Applies(Master(TestGroup.FollowerId), [Follower()], _now));
        Assert.False(GameFollowing.Applies(Master() with { Party = null }, [Follower()], _now));
        Assert.False(GameFollowing.Applies(Master(), [], _now));
    }
}
