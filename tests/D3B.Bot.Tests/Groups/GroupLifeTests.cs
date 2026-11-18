using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Groups;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

/// <summary>The group's life before an attack: the master attacks once each follower's life is over its profile's share, each follower healing by itself (D433).</summary>
public sealed class GroupLifeTests
{
    // The master's party holds the follower, as the server tells both once the follower accepted.
    private static void Joined(TestConnection master, TestConnection follower)
    {
        master.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId));
        master.Server(new PartyNewMemberEvent { PartyId = TestGroup.PartyId, Member = new Character { Id = TestGroup.FollowerId } });
        follower.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));
    }

    private static ActivityRun Follow(TestPlay play, BotActor follower)
    {
        return follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));
    }

    private static string Doing(ActivityRun run)
    {
        return TestGroup.Say(run.Activity.Report?.Doing);
    }

    private static async Task<(TestConnection Master, BotActor MasterBot, TestConnection Follower, BotActor FollowerBot, ActivityRun Run)> GroupAsync(TestPlay play, int life, params InventoryObject[] carried)
    {
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);

        if (carried.Length > 0)
            connection.Server(new InventoryContentEvent { Objects = { carried } });

        // Two points a second, as measured (D153).
        connection.Server(new LifeRegenEndEvent { Life = life, MaxLife = 1_000 });
        connection.Server(new LifeRegenStartEvent { Interval = 5 });
        Joined(masterConnection, connection);

        var run = Follow(play, follower);

        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 2));

        return (masterConnection, master, connection, follower, run);
    }

    [Fact]
    public async Task AMasterAttacksOnlyOnceItsFollowerRegeneratedToItsProfilesShare()
    {
        await using var play = new TestPlay();
        var (masterConnection, master, connection, _, run) = await GroupAsync(play, 600);
        var started = play.Time.GetUtcNow();
        var leading = master.StartActivity(new AttackingActivity { HealBelow = 70 });

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "the master waiting");

        Assert.Equal(new MissingMember(TestGroup.Follower, "Follower", MemberAbsence.Healing), Assert.Single(master.WaitingFor));
        Assert.Equal("Waits for Follower, who regains its life", TestGroup.Say(GroupTexts.Waiting(master.WaitingFor)));
        await TestGroup.UntilAsync(play, () => Doing(run).StartsWith("Waits for its life to regenerate", StringComparison.Ordinal), "the follower regenerating");

        // 100 points to 700, two a second since the regeneration started, just before: the attack about 50 s later, with
        // nothing eaten.
        Assert.IsType<AttackMonsterRequest>(await TestGroup.SentAsync(play, masterConnection, 1));
        Assert.InRange(play.Time.GetUtcNow() - started, TimeSpan.FromSeconds(48), TimeSpan.FromSeconds(51));
        Assert.Equal(2, connection.Sent.Count);
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AFollowerEatsWhatOnlyHealsBeforeItsMasterAttacks()
    {
        await using var play = new TestPlay();
        var (masterConnection, master, connection, _, run) = await GroupAsync(
            play,
            300,
            new InventoryObject { Item = new ObjectItem { Uid = 52, ItemId = TestPlay.ClassPotion, Quantity = 1 }, Position = InventoryItem.NotWorn },
            new InventoryObject { Item = new ObjectItem { Uid = 50, ItemId = TestPlay.Bread, Quantity = 3 }, Position = InventoryItem.NotWorn });
        var started = play.Time.GetUtcNow();
        var leading = master.StartActivity(new AttackingActivity { HealBelow = 70 });

        // As a profile choosing no food (D148): the bread, never the potion whose use does more than heal.
        Assert.Equal(new ObjectUseMultipleRequest { Uid = 50, Quantity = 3 }, await TestGroup.SentAsync(play, connection, 3));
        await TestGroup.SettleAsync(master);

        Assert.Empty(masterConnection.Sent);

        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 50, Quantity = 1 } });
        connection.Server(new LifeRegenEndEvent { Life = 1_000, MaxLife = 1_000 });

        // As the follower ate, not 200 s later as its life would have regenerated to the share.
        Assert.IsType<AttackMonsterRequest>(await TestGroup.SentAsync(play, masterConnection, 1));
        Assert.True(play.Time.GetUtcNow() - started < TimeSpan.FromSeconds(20));
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AMasterWhoseProfileHealsNotAttacksAtOnceWhateverItsFollowersLife()
    {
        await using var play = new TestPlay();
        var (masterConnection, master, connection, _, run) = await GroupAsync(play, 100);
        var started = play.Time.GetUtcNow();
        var leading = master.StartActivity(new AttackingActivity { HealBelow = null });

        Assert.IsType<AttackMonsterRequest>(await TestGroup.SentAsync(play, masterConnection, 1));
        Assert.True(play.Time.GetUtcNow() - started < TimeSpan.FromSeconds(5));
        Assert.Equal(2, connection.Sent.Count);
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AMasterWaitsForItsFollowersLifeBeforeAWalkToo()
    {
        // As an activity heals before its step (D441): held only before the attack, the master would wait on the group's
        // cell, which the group leaves meanwhile.
        await using var play = new TestPlay();
        var (masterConnection, master, _, _, run) = await GroupAsync(play, 600);
        var started = play.Time.GetUtcNow();
        var leading = master.StartActivity(new AttackingActivity { HealBelow = 70, Request = new MapChangeRequest { MapId = (int)TestPlay.NorthMapId } });

        Assert.IsType<MapChangeRequest>(await TestGroup.SentAsync(play, masterConnection, 1));
        Assert.InRange(play.Time.GetUtcNow() - started, TimeSpan.FromSeconds(48), TimeSpan.FromSeconds(51));
        leading.Stop();
        run.Stop();
    }
}
