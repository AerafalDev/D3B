using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Dungeons;
using D3B.Bot.Groups;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Groups;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Dungeons;

/// <summary>The dungeon activity of a group's master (D528): a dungeon a follower cannot enter is left out of the group's turn.</summary>
public sealed class DungeonActivityGroupTests
{
    private static string Said(ActivityRun run)
    {
        var reason = Assert.IsType<ActivityException>(run.Failure).Reason!;
        var english = CultureInfo.GetCultureInfo("en");

        return string.Format(english, reason.Format.In(english), [.. reason.Values.Select(value => value.Text ?? (object)value.Id)]);
    }

    private static InventoryContentEvent Carrying(int itemId)
    {
        return new InventoryContentEvent { Objects = { new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = itemId, Quantity = 1 }, Position = InventoryItem.NotWorn } } };
    }

    private static async Task<(BotActor Master, ActivityRun Follow)> GroupAsync(TestPlay play, bool followerKey)
    {
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);

        masterConnection.Server(Carrying(TestPlay.DungeonKeyId));
        connection.Server(Carrying(followerKey ? TestPlay.DungeonKeyId : TestPlay.Ash));
        await master.ProcessedAsync();
        await follower.ProcessedAsync();

        var run = follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));

        await TestGroup.UntilAsync(play, () => play.Bots.Groups.FollowersOf(TestGroup.Master).Count is 1, "the follower in its master's group");

        return (master, run);
    }

    [Fact]
    public async Task ADungeonAFollowerCarriesNoKeyForIsLeftOutSayingWhichFollower()
    {
        // The owner's choice (D528): rather than entering without it, the master leaves the dungeon out of the group's turn.
        await using var play = new TestPlay();
        var (master, follow) = await GroupAsync(play, followerKey: false);
        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("Follower, who follows the character, carries neither the dungeon's key nor a keyring that opens it.", Said(run));
        Assert.True(Assert.IsType<ActivityException>(run.Failure).IsFinal);
        follow.Stop();
    }

    [Fact]
    public async Task AGroupWhoseFollowersCarryTheKeyGoesToTheDungeon()
    {
        await using var play = new TestPlay();
        var (master, follow) = await GroupAsync(play, followerKey: true);
        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        await TestGroup.UntilAsync(play, () => TestGroup.Say(run.Activity.Report?.Doing) == "On the way to the dungeon's door", "going to the door");
        Assert.False(run.IsOver);
        run.Stop();
        follow.Stop();
    }
}
