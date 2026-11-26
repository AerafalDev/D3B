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

    private static async Task<(BotActor Master, ActivityRun Follow, TestConnection Follower, TestConnection MasterConnection)> GroupAsync(TestPlay play, bool followerKey, int followerLevel = 200)
    {
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, level: followerLevel);

        masterConnection.Server(Carrying(TestPlay.DungeonKeyId));
        connection.Server(Carrying(followerKey ? TestPlay.DungeonKeyId : TestPlay.Ash));
        await master.ProcessedAsync();
        await follower.ProcessedAsync();

        var run = follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));

        await TestGroup.UntilAsync(play, () => play.Bots.Groups.FollowersOf(TestGroup.Master).Count is 1, "the follower in its master's group");

        return (master, run, connection, masterConnection);
    }

    // Both told, as the captures measured each change of an exchange (D435, D442).
    private static void Both(TestConnection master, TestConnection follower, Google.Protobuf.IMessage message)
    {
        master.Server(message);
        follower.Server(message);
    }

    private static InventoryObject Stack(int uid, int itemId, int quantity)
    {
        return new InventoryObject { Item = new ObjectItem { Uid = uid, ItemId = itemId, Quantity = quantity }, Position = InventoryItem.NotWorn };
    }

    [Fact]
    public async Task ADungeonAFollowerCarriesNoKeyForAndTheMasterNoneToSpareIsLeftOutSayingWhichFollower()
    {
        // The owner's choice (D528): rather than entering without it, the master leaves the dungeon out of the group's turn. Its
        // one key is the one its own entry needs, which it never gives (D569).
        await using var play = new TestPlay();
        var (master, follow, follower, masterConnection) = await GroupAsync(play, followerKey: false);
        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("Follower, who follows the character, carries neither the dungeon's key nor a keyring that opens it, and the character has no key to spare.", Said(run));
        Assert.True(Assert.IsType<ActivityException>(run.Failure).IsFinal);
        Assert.DoesNotContain(follower.Sent, request => request is ExchangePlayerRequest);
        Assert.DoesNotContain(masterConnection.Sent, request => request is ExchangeAcceptRequest or ExchangeObjectMoveRequest);
        follow.Stop();
    }

    [Fact]
    public async Task AMasterCarryingTheKeyringSparesItsKeyForAKeylessFollower()
    {
        // The keyring opens the dungeon for the master, which then needs none of its keys (D523, D569).
        await using var play = new TestPlay();
        var (master, follow, _, masterConnection) = await GroupAsync(play, followerKey: false);

        masterConnection.Server(new ObjectAddedEvent { Object = Stack(13, TestPlay.KeyringId, 1) });
        await master.ProcessedAsync();

        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        await TestGroup.UntilAsync(play, () => TestGroup.Say(run.Activity.Report?.Doing) == "On the way to the dungeon's door", "going to the door");
        Assert.False(run.IsOver);
        run.Stop();
        follow.Stop();
    }

    [Fact]
    public async Task AtTheDoorTheMasterGivesItsSpareKeyToAKeylessFollowerByAnExchangeAndBothEnter()
    {
        // The owner's ask and choice (D569): the master keeps the key its own entry needs and gives the follower the one it
        // lacks, the follower asking the exchange as at the group's bank (D442), the master moving the key in.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300, TestPlay.InsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.InsideMapId);

        masterConnection.Server(new InventoryContentEvent { Objects = { Stack(12, TestPlay.DungeonKeyId, 2) } });
        connection.Server(new InventoryContentEvent { Objects = { Stack(40, TestPlay.Ash, 1) } });
        masterConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId));
        masterConnection.Server(new PartyNewMemberEvent { PartyId = TestGroup.PartyId, Member = new Character { Id = TestGroup.FollowerId } });
        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        var follow = follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));

        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 2));

        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        Assert.Equal(new ExchangePlayerRequest { TargetId = TestGroup.MasterId }, await TestGroup.SentAsync(play, connection, 3));
        Assert.Equal("Takes a key from Master", TestGroup.Say(follow.Activity.Report?.Doing));
        Assert.Equal("Gives a key to Follower", TestGroup.Say(run.Activity.Report?.Doing));
        Both(masterConnection, connection, new ExchangeRequestedEvent { Type = ExchangeType.Player, SourceId = TestGroup.FollowerId, TargetId = TestGroup.MasterId });

        Assert.IsType<ExchangeAcceptRequest>(await TestGroup.SentAsync(play, masterConnection, 1));
        Both(masterConnection, connection, new ExchangeStartedWithPlayerEvent { Type = ExchangeType.Player });

        // One key of its stack of two: a part of a stack goes by a move, the only request naming a quantity (D482).
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 12, Quantity = 1 }, await TestGroup.SentAsync(play, masterConnection, 2));
        Both(masterConnection, connection, new ExchangeObjectsAddedEvent());

        var changed = play.Time.GetUtcNow();

        Assert.Equal(new ExchangeReadyRequest { Step = 1, Ready = true }, await TestGroup.SentAsync(play, masterConnection, 3));

        // Measured on the owner's test of 2026-11-19 (D458): the server takes no readiness sooner after the last change.
        Assert.True(play.Time.GetUtcNow() - changed >= TimeSpan.FromSeconds(3.5));
        Both(masterConnection, connection, new ExchangeReadyEvent { Ready = true, CharacterId = TestGroup.MasterId });

        Assert.Equal(new ExchangeReadyRequest { Step = 1, Ready = true }, await TestGroup.SentAsync(play, connection, 4));
        Both(masterConnection, connection, new ExchangeReadyEvent { Ready = false, CharacterId = TestGroup.FollowerId });
        Both(masterConnection, connection, new ExchangeReadyEvent { Ready = false, CharacterId = TestGroup.MasterId });
        Both(masterConnection, connection, new ExchangeLeaveEvent());

        // Each bot hears its own inventory from its own connection, in an order not measured: the master waits for both, so
        // that it neither gives a second key nor reads its follower still keyless.
        await TestGroup.SettleAsync(master, follower);
        masterConnection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 12, Quantity = 1 } });
        await TestGroup.SettleAsync(master, follower);
        Assert.Equal(3, masterConnection.Sent.Count);
        connection.Server(new ObjectAddedEvent { Object = Stack(46, TestPlay.DungeonKeyId, 1) });

        // Given, the master talks to the keeper with the key it kept, and enters.
        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestGroup.SentAsync(play, masterConnection, 4));
        masterConnection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        masterConnection.Server(new NpcDialogQuestionEvent { MessageId = 13_769, Replies = { new NpcReply { ReplyId = TestPlay.KeyReplyId } } });
        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.KeyReplyId }, await TestGroup.SentAsync(play, masterConnection, 5));
        masterConnection.Server(new NpcDialogQuestionEvent { MessageId = 17_460, Replies = { new NpcReply { ReplyId = TestPlay.ConfirmReplyId } } });
        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ConfirmReplyId }, await TestGroup.SentAsync(play, masterConnection, 6));
        masterConnection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        masterConnection.Server(new ObjectDeletedEvent { Uid = 12 });
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.OtherInsideMapId, 300);

        // The follower, its key given, enters behind its master by itself (D528).
        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestGroup.SentAsync(play, connection, 5));
        Assert.Equal(TestPlay.DungeonId, run.Activity.DungeonInside);
        run.Stop();
        follow.Stop();
    }

    [Fact]
    public async Task AnExchangeThatClosesWithNoKeyMovedIsNoKeyGivenAndThreeShutTheDungeon()
    {
        // Such as the player closing the window by hand (D455): the exchange is left, nothing moved (D569).
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300, TestPlay.InsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.InsideMapId);

        masterConnection.Server(new InventoryContentEvent { Objects = { Stack(12, TestPlay.DungeonKeyId, 2) } });
        connection.Server(new InventoryContentEvent { Objects = { Stack(40, TestPlay.Ash, 1) } });
        masterConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId));
        masterConnection.Server(new PartyNewMemberEvent { PartyId = TestGroup.PartyId, Member = new Character { Id = TestGroup.FollowerId } });
        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        var follow = follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));

        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 2));

        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        for (var round = 0; round < 3; round++)
        {
            Assert.IsType<ExchangePlayerRequest>(await TestGroup.SentAsync(play, connection, 3 + (2 * round)));
            Both(masterConnection, connection, new ExchangeRequestedEvent { Type = ExchangeType.Player, SourceId = TestGroup.FollowerId, TargetId = TestGroup.MasterId });
            Assert.IsType<ExchangeAcceptRequest>(await TestGroup.SentAsync(play, masterConnection, 1 + (3 * round)));
            Both(masterConnection, connection, new ExchangeStartedWithPlayerEvent { Type = ExchangeType.Player });
            Assert.IsType<ExchangeObjectMoveRequest>(await TestGroup.SentAsync(play, masterConnection, 2 + (3 * round)));
            Both(masterConnection, connection, new ExchangeObjectsAddedEvent());
            Assert.IsType<ExchangeReadyRequest>(await TestGroup.SentAsync(play, masterConnection, 3 + (3 * round)));
            Both(masterConnection, connection, new ExchangeReadyEvent { Ready = true, CharacterId = TestGroup.MasterId });
            Assert.IsType<ExchangeReadyRequest>(await TestGroup.SentAsync(play, connection, 4 + (2 * round)));
            Both(masterConnection, connection, new ExchangeLeaveEvent());
        }

        await TestGroup.UntilAsync(play, () => run.IsOver, "the activity over");
        Assert.Equal("Follower, who follows the character, took none of the keys the character gives it at the dungeon's door.", Said(run));
        Assert.Equal(3, connection.Sent.Count(request => request is ExchangePlayerRequest));
        Assert.DoesNotContain(masterConnection.Sent, request => request is NpcActionRequest);
        follow.Stop();
    }

    [Fact]
    public async Task TwoKeylessFollowersAndOneSpareKeyLeaveTheDungeonOutNamingTheOneItDoesNotCover()
    {
        // The spare keys cover the followers in the chain's order, each key once (D569): the second is not covered.
        await using var play = new TestPlay();
        var (master, follow, _, masterConnection) = await GroupAsync(play, followerKey: false);
        var (secondConnection, second) = await TestGroup.SecondAsync(play, 302);

        masterConnection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 12, Quantity = 2 } });
        secondConnection.Server(Carrying(TestPlay.Ash));
        await master.ProcessedAsync();
        await second.ProcessedAsync();

        var secondRun = second.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));

        await TestGroup.UntilAsync(play, () => play.Bots.Groups.FollowersOf(TestGroup.Master).Count is 2, "both followers in the group");

        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("Second, who follows the character, carries neither the dungeon's key nor a keyring that opens it, and the character has no key to spare.", Said(run));
        secondRun.Stop();
        follow.Stop();
    }

    [Fact]
    public async Task AFollowerThatNeverTakesTheKeyShutsTheDungeonAfterThreeExchangesRatherThanWaitingAtItsDoorForEver()
    {
        // The server answers none of the follower's askings: each round waits 30 s for it, as the group's bank does (D442, D569).
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300, TestPlay.InsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.InsideMapId);

        masterConnection.Server(new InventoryContentEvent { Objects = { Stack(12, TestPlay.DungeonKeyId, 2) } });
        connection.Server(new InventoryContentEvent { Objects = { Stack(40, TestPlay.Ash, 1) } });
        masterConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId));
        masterConnection.Server(new PartyNewMemberEvent { PartyId = TestGroup.PartyId, Member = new Character { Id = TestGroup.FollowerId } });
        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        var follow = follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));

        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 2));

        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        await TestGroup.UntilAsync(play, () => run.IsOver, "the activity over");
        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("Follower, who follows the character, took none of the keys the character gives it at the dungeon's door.", Said(run));
        Assert.Equal(3, connection.Sent.Count(request => request is ExchangePlayerRequest));
        Assert.DoesNotContain(masterConnection.Sent, request => request is NpcActionRequest);
        follow.Stop();
    }

    [Fact]
    public async Task ADungeonAFollowerLacksTheLevelForIsLeftOutSayingItsLevel()
    {
        await using var play = new TestPlay();
        var (master, follow, _, _) = await GroupAsync(play, followerKey: true, followerLevel: 10);
        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("Follower, who follows the character, is level 10, and the dungeon lets in from level 20.", Said(run));
        follow.Stop();
    }

    [Fact]
    public async Task AFollowerNotConnectedKeepsNoDungeonShut()
    {
        // A follower's state disconnected says nothing of what it carries now; its master goes on without it (D490).
        await using var play = new TestPlay();
        var (master, follow, connection, _) = await GroupAsync(play, followerKey: false);

        Assert.True(play.Bots.TryGet(TestGroup.Follower, out var follower));
        connection.Close();
        await TestGroup.UntilAsync(play, () => !follower.IsConnected, "the follower disconnected");

        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        await TestGroup.UntilAsync(play, () => TestGroup.Say(run.Activity.Report?.Doing) == "On the way to the dungeon's door", "going to the door");
        Assert.False(run.IsOver);
        run.Stop();
        follow.Stop();
    }

    [Fact]
    public async Task AGroupWhoseFollowersCarryTheKeyGoesToTheDungeon()
    {
        await using var play = new TestPlay();
        var (master, follow, _, _) = await GroupAsync(play, followerKey: true);
        var run = master.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.DungeonId], RestartAfterFailure = false }, play.Tables, bots: play.Bots));

        await TestGroup.UntilAsync(play, () => TestGroup.Say(run.Activity.Report?.Doing) == "On the way to the dungeon's door", "going to the door");
        Assert.False(run.IsOver);
        run.Stop();
        follow.Stop();
    }
}
