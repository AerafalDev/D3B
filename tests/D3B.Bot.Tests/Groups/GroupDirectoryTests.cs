using D3B.Bot.Groups;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Groups;

public sealed class GroupDirectoryTests
{
    [Fact]
    public async Task AFollowerIsInItsMastersGroupUntilItsMembershipIsDisposed()
    {
        await using var play = new TestPlay();
        var (_, follower) = await TestGroup.FollowerAsync(play, 300);
        var groups = play.Bots.Groups;
        var changed = groups.Changed;

        var membership = groups.Follow(follower, TestGroup.Master, out _);

        Assert.NotNull(membership);
        Assert.True(changed.IsCompleted);
        Assert.Same(follower, Assert.Single(groups.FollowersOf(TestGroup.Master)));
        Assert.Equal(TestGroup.Master, groups.MasterOf(TestGroup.Follower));

        changed = groups.Changed;
        membership.Dispose();

        Assert.True(changed.IsCompleted);
        Assert.Empty(groups.FollowersOf(TestGroup.Master));
        Assert.Null(groups.MasterOf(TestGroup.Follower));
    }

    [Fact]
    public async Task ACharacterCannotFollowItself()
    {
        await using var play = new TestPlay();
        var (_, follower) = await TestGroup.FollowerAsync(play, 300);

        Assert.Null(play.Bots.Groups.Follow(follower, TestGroup.Follower, out var refusal));
        Assert.Equal(FollowRefusal.Itself, refusal);
    }

    [Fact]
    public async Task NoFollowerLeadsNorDoesAMasterFollow()
    {
        await using var play = new TestPlay();
        var (_, follower) = await TestGroup.FollowerAsync(play, 300);
        var (_, second) = await TestGroup.SecondAsync(play, 301);
        var groups = play.Bots.Groups;

        using var membership = groups.Follow(follower, TestGroup.Master, out _);

        // The second would follow a follower, or the follower, a master, another.
        Assert.Null(groups.Follow(second, TestGroup.Follower, out var refusal));
        Assert.Equal(FollowRefusal.MasterFollows, refusal);
        Assert.Null(groups.Follow(follower, TestGroup.Second, out refusal));
        Assert.Equal(FollowRefusal.Follows, refusal);

        using var other = groups.Follow(second, TestGroup.Master, out _);

        Assert.NotNull(other);
    }

    [Fact]
    public async Task AMasterOfFollowersCannotFollow()
    {
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);
        var groups = play.Bots.Groups;

        using var membership = groups.Follow(follower, TestGroup.Master, out _);

        Assert.Null(groups.Follow(master, TestGroup.Second, out var refusal));
        Assert.Equal(FollowRefusal.Leads, refusal);
    }

    [Fact]
    public async Task AMasterLeadsSevenFollowersAtMost()
    {
        await using var play = new TestPlay();
        var groups = play.Bots.Groups;
        var memberships = new List<IDisposable>();

        for (var at = 0; at < GroupDirectory.MostFollowers; at++)
        {
            var (_, follower) = await play.EnterAsync(300, TestPlay.MapId, new D3B.Bot.Network.GameAccount(100 + at, "f", "t"), 1_000 + at, $"F{at}");

            memberships.Add(groups.Follow(follower, TestGroup.Master, out _)!);
        }

        var (_, eighth) = await TestGroup.FollowerAsync(play, 300);

        Assert.Null(groups.Follow(eighth, TestGroup.Master, out var refusal));
        Assert.Equal(FollowRefusal.Full, refusal);

        foreach (var membership in memberships)
            membership.Dispose();
    }
}
