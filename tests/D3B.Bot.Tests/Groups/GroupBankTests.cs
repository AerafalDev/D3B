using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Groups;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

/// <summary>The group's bank: a follower full takes the group to the bank, gives the master what it carries by an exchange, and the master deposits it with its own loot (D433, D442).</summary>
public sealed class GroupBankTests
{
    private const int MastersLoot = 1_782;
    private const int MastersKept = 30_449;
    private const int AshUid = 40;
    private const int GivenUid = 50;

    private static readonly NpcActionRequest _talk = new() { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId };

    private static InventoryObject Stack(int uid, int itemId, int quantity, int position = InventoryItem.NotWorn)
    {
        return new InventoryObject { Item = new ObjectItem { Uid = uid, ItemId = itemId, Quantity = quantity }, Position = position };
    }

    // Both told, as the captures measured each (D435, D442).
    private static void Both(TestConnection master, TestConnection follower, Google.Protobuf.IMessage message)
    {
        master.Server(message);
        follower.Server(message);
    }

    // The master and its follower in the bank, in one party, the follower following and full, the master's pods as given.
    private static async Task<(TestConnection Master, BotActor MasterBot, TestConnection Follower, BotActor FollowerBot, ActivityRun Run)> InTheBankAsync(TestPlay play, int mastersWeight)
    {
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 396, TestPlay.InsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 397, TestPlay.InsideMapId);

        masterConnection.Server(new InventoryContentEvent { Objects = { Stack(12, MastersLoot, 21), Stack(14, MastersKept, 3) } });
        masterConnection.Server(new InventoryWeightEvent { Weight = mastersWeight, MaxWeight = 1_000 });
        connection.Server(new InventoryContentEvent { Kamas = 500, Objects = { Stack(AshUid, TestPlay.Ash, 10), Stack(41, 2_400, 1, position: 1) } });
        connection.Server(new InventoryWeightEvent { Weight = 950, MaxWeight = 1_000 });
        masterConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId));
        masterConnection.Server(new PartyNewMemberEvent { PartyId = TestGroup.PartyId, Member = new Character { Id = TestGroup.FollowerId } });
        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        var run = follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));

        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 2));

        return (masterConnection, master, connection, follower, run);
    }

    // The banker's dialog and the bank opened, in the order of the captures (D80).
    private static void Opened(TestConnection connection)
    {
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { new NpcReply { ReplyId = TestPlay.ChestReplyId } } });
    }

    private static void BankOpened(TestConnection connection)
    {
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent());
    }

    // The follower asks, the master accepts, the follower moves its ash in and says it is ready, then the master: the exchange as captured.
    private static async Task ExchangedAsync(TestPlay play, TestConnection master, TestConnection follower, int masterSent, int followerSent, int quantity)
    {
        Assert.Equal(new ExchangePlayerRequest { TargetId = TestGroup.MasterId }, await TestGroup.SentAsync(play, follower, followerSent + 1));
        Both(master, follower, new ExchangeRequestedEvent { Type = ExchangeType.Player, SourceId = TestGroup.FollowerId, TargetId = TestGroup.MasterId });

        Assert.IsType<ExchangeAcceptRequest>(await TestGroup.SentAsync(play, master, masterSent + 1));
        Both(master, follower, new ExchangeStartedWithPlayerEvent { Type = ExchangeType.Player });

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = AshUid, Quantity = quantity }, await TestGroup.SentAsync(play, follower, followerSent + 2));
        Both(master, follower, new ExchangeObjectsAddedEvent());

        Assert.Equal(new ExchangeReadyRequest { Step = 1, Ready = true }, await TestGroup.SentAsync(play, follower, followerSent + 3));
        Both(master, follower, new ExchangeReadyEvent { Ready = true, CharacterId = TestGroup.FollowerId });

        Assert.Equal(new ExchangeReadyRequest { Step = 1, Ready = true }, await TestGroup.SentAsync(play, master, masterSent + 2));
        master.Server(new ObjectAddedEvent { Object = Stack(GivenUid, TestPlay.Ash, quantity) });
        master.Server(new InventoryWeightEvent { Weight = 150, MaxWeight = 1_000 });
        follower.Server(new ObjectDeletedEvent { Uid = AshUid });
        follower.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        Both(master, follower, new ExchangeReadyEvent { Ready = false, CharacterId = TestGroup.FollowerId });
        Both(master, follower, new ExchangeReadyEvent { Ready = false, CharacterId = TestGroup.MasterId });
        Both(master, follower, new ExchangeLeaveEvent());
    }

    private static async Task DepositedAsync(TestPlay play, TestConnection master, int masterSent, params int[] uids)
    {
        Assert.Equal(_talk, await TestGroup.SentAsync(play, master, masterSent + 1));
        Opened(master);
        Assert.IsType<NpcDialogReplyRequest>(await TestGroup.SentAsync(play, master, masterSent + 2));
        BankOpened(master);
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { uids } }, await TestGroup.SentAsync(play, master, masterSent + 3));
        master.Server(new ObjectsDeletedEvent { Uids = { uids } });
        master.Server(new InventoryWeightEvent { Weight = 20, MaxWeight = 1_000 });
        Assert.IsType<DialogLeaveRequest>(await TestGroup.SentAsync(play, master, masterSent + 4));
        master.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });
    }

    [Fact]
    public async Task AMasterEmptiesItselfThenTakesItsFollowersObjectsThenDepositsThem()
    {
        // The owner's report and choice (D452): the master deposits all it does not wear first, its own loot or not, then
        // takes the ash, never the follower's worn stack nor its kamas, then deposits it.
        await using var play = new TestPlay();
        var (masterConnection, master, connection, _, run) = await InTheBankAsync(play, 100);
        var activity = new BankingActivity { Loot = new HashSet<int> { MastersLoot } };
        var leading = master.StartActivity(activity);

        await DepositedAsync(play, masterConnection, 0, 12, 14);
        await ExchangedAsync(play, masterConnection, connection, 4, 2, 10);
        await DepositedAsync(play, masterConnection, 6, GivenUid);
        await TestGroup.SettleAsync(master);

        Assert.DoesNotContain(connection.Sent, request => request is ExchangeKamasRequest);
        Assert.DoesNotContain(connection.Sent, request => request is ExchangeObjectMoveRequest { Uid: 41 });
        Assert.Equal(0, activity.OwnUnloads);
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AStackTheBankRefusesStopsNotTheMasterTakingItsFollowersObjects()
    {
        // The owner's report from game (D457): the bank took none of the one stack its master carried, such as a quest's
        // object, and the master stopped there, its full follower never emptied.
        await using var play = new TestPlay();
        var (masterConnection, master, connection, _, run) = await InTheBankAsync(play, 100);
        var leading = master.StartActivity(new BankingActivity { Loot = new HashSet<int> { MastersLoot } });

        Assert.Equal(_talk, await TestGroup.SentAsync(play, masterConnection, 1));
        Opened(masterConnection);
        Assert.IsType<NpcDialogReplyRequest>(await TestGroup.SentAsync(play, masterConnection, 2));
        BankOpened(masterConnection);
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 12, 14 } }, await TestGroup.SentAsync(play, masterConnection, 3));

        // The server answers nothing: it took none.
        Assert.IsType<DialogLeaveRequest>(await TestGroup.SentAsync(play, masterConnection, 4));
        masterConnection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        await ExchangedAsync(play, masterConnection, connection, 4, 2, 10);

        // What it was given, and not again the stacks the bank refused.
        await DepositedAsync(play, masterConnection, 6, GivenUid);
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AStackTheBankLeftIsNotOfferedAgainInTheSameUnloading()
    {
        await using var play = new TestPlay();
        var (masterConnection, master, connection, _, run) = await InTheBankAsync(play, 100);
        var leading = master.StartActivity(new BankingActivity { Loot = new HashSet<int> { MastersLoot } });

        Assert.Equal(_talk, await TestGroup.SentAsync(play, masterConnection, 1));
        Opened(masterConnection);
        Assert.IsType<NpcDialogReplyRequest>(await TestGroup.SentAsync(play, masterConnection, 2));
        BankOpened(masterConnection);
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 12, 14 } }, await TestGroup.SentAsync(play, masterConnection, 3));

        // The bank takes one stack of two, and leaves the other.
        masterConnection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        masterConnection.Server(new InventoryWeightEvent { Weight = 60, MaxWeight = 1_000 });
        Assert.IsType<DialogLeaveRequest>(await TestGroup.SentAsync(play, masterConnection, 4));
        masterConnection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        await ExchangedAsync(play, masterConnection, connection, 4, 2, 10);
        await DepositedAsync(play, masterConnection, 6, GivenUid);
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AFullMasterDepositsBeforeItTakesItsFollowersObjects()
    {
        // Measured in game (D452): full, the master took its follower's objects all the same, and the exchange never concluded.
        await using var play = new TestPlay();
        var (masterConnection, master, connection, _, run) = await InTheBankAsync(play, 1_000);
        var leading = master.StartActivity(new BankingActivity { Loot = new HashSet<int> { MastersLoot } });

        await DepositedAsync(play, masterConnection, 0, 12, 14);

        Assert.Equal(2, connection.Sent.Count);

        await ExchangedAsync(play, masterConnection, connection, 4, 2, 10);
        await DepositedAsync(play, masterConnection, 6, GivenUid);
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AMasterNobodyFollowsUnloadsAsItsActivitySays()
    {
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, 396, TestPlay.InsideMapId);

        connection.Server(new InventoryWeightEvent { Weight = 950, MaxWeight = 1_000 });

        var activity = new BankingActivity();
        var leading = master.StartActivity(activity);

        await TestGroup.UntilAsync(play, () => activity.OwnUnloads > 0, "the activity's own unloading");

        Assert.Empty(connection.Sent);
        leading.Stop();
    }

    [Fact]
    public async Task AMasterWaitsForAFollowerMovingManyStacksAsLongAsEachMoveComes()
    {
        // Each move after the client's display time, here 20 s: three take longer than one wait of 30 s, which each move renews.
        await using var play = new TestPlay();
        var (masterConnection, master, connection, follower, run) = await InTheBankAsync(play, 100);

        connection.Server(new InventoryContentEvent { Objects = { Stack(AshUid, TestPlay.Ash, 2), Stack(42, TestPlay.Chestnut, 2), Stack(43, TestPlay.WheatBag, 2) } });
        await follower.ProcessedAsync();

        var leading = master.StartActivity(new BankingActivity { Loot = new HashSet<int> { MastersLoot } });

        await DepositedAsync(play, masterConnection, 0, 12, 14);
        play.Bots.Delays = BotDelays.Measured with { Display = TimeSpan.FromSeconds(20), BetweenSteps = TimeSpan.Zero };

        Assert.IsType<ExchangePlayerRequest>(await TestGroup.SentAsync(play, connection, 3));
        Both(masterConnection, connection, new ExchangeRequestedEvent { Type = ExchangeType.Player, SourceId = TestGroup.FollowerId, TargetId = TestGroup.MasterId });
        Assert.IsType<ExchangeAcceptRequest>(await TestGroup.SentAsync(play, masterConnection, 5));
        Both(masterConnection, connection, new ExchangeStartedWithPlayerEvent { Type = ExchangeType.Player });

        var opened = play.Time.GetUtcNow();

        for (var move = 0; move < 3; move++)
        {
            Assert.IsType<ExchangeObjectMoveRequest>(await TestGroup.SentAsync(play, connection, 4 + move));
            Both(masterConnection, connection, new ExchangeObjectsAddedEvent());
        }

        Assert.Equal(new ExchangeReadyRequest { Step = 3, Ready = true }, await TestGroup.SentAsync(play, connection, 7));
        Assert.True(play.Time.GetUtcNow() - opened > TimeSpan.FromSeconds(30));
        Both(masterConnection, connection, new ExchangeReadyEvent { Ready = true, CharacterId = TestGroup.FollowerId });

        Assert.Equal(new ExchangeReadyRequest { Step = 3, Ready = true }, await TestGroup.SentAsync(play, masterConnection, 6));
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task TheMasterKeepsTheFoodItEatsAtTheBank()
    {
        // The owner's choice (D452): all it does not wear, but the food that only heals, which it eats (D441).
        await using var play = new TestPlay();
        var (masterConnection, master, _, _, run) = await InTheBankAsync(play, 100);

        masterConnection.Server(new ObjectAddedEvent { Object = Stack(16, TestPlay.Bread, 5) });
        await master.ProcessedAsync();

        var leading = master.StartActivity(new BankingActivity { Loot = new HashSet<int> { MastersLoot } });

        await DepositedAsync(play, masterConnection, 0, 12, 14);
        leading.Stop();
        run.Stop();
    }

    [Fact]
    public async Task AFollowerThatNeverAsksIsLeftAndTheMasterWaitsNoLongerThanThirtySeconds()
    {
        // A follower in the group whose activity does not run: it asks nothing, and the master waits no longer than 30 s.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 396, TestPlay.InsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 397, TestPlay.InsideMapId);

        masterConnection.Server(new InventoryContentEvent { Objects = { Stack(12, MastersLoot, 21) } });
        masterConnection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
        connection.Server(new InventoryContentEvent { Objects = { Stack(AshUid, TestPlay.Ash, 10) } });
        connection.Server(new InventoryWeightEvent { Weight = 950, MaxWeight = 1_000 });
        masterConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId));
        masterConnection.Server(new PartyNewMemberEvent { PartyId = TestGroup.PartyId, Member = new Character { Id = TestGroup.FollowerId } });
        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);

        await TestGroup.SettleAsync(master, follower);

        var leading = master.StartActivity(new BankingActivity { Loot = new HashSet<int> { MastersLoot } });

        await DepositedAsync(play, masterConnection, 0, 12);

        for (var wait = 0; wait < 8; wait++)
        {
            play.Time.Advance(TimeSpan.FromSeconds(5));
            await TestGroup.SettleAsync(master, follower);
        }

        Assert.DoesNotContain(masterConnection.Sent, request => request is ExchangeAcceptRequest);
        Assert.Equal(4, masterConnection.Sent.Count);
        leading.Stop();
    }
}
