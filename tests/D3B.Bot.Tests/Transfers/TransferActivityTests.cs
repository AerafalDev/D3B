using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Transfers;
using D3B.Data.Items;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Transfers;

public sealed class TransferActivityTests
{
    private const int ChestInstanceId = 33_001;
    private const int BankAsh = 500;
    private const int BankBread = 501;

    private static readonly NpcActionRequest _talk = new() { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId };

    private static InventoryObject Stack(int uid, int itemId, int quantity)
    {
        return new InventoryObject { Item = new ObjectItem { Uid = uid, ItemId = itemId, Quantity = quantity }, Position = InventoryItem.NotWorn };
    }

    /// <summary>The bank's inside, its banker and the guild's chest on one map, as in Astrub's bank (D153).</summary>
    private static async Task<(TestConnection Connection, BotActor Bot)> InTheBankAsync(TestPlay play, int weight = 400, params InventoryObject[] carried)
    {
        var (connection, bot) = await play.EnterAsync(301, TestPlay.InsideMapId);
        var information = TestPlay.Information(301, TestPlay.InsideMapId);

        information.InteractiveElements.Add(new InteractiveElement
        {
            ElementId = TestPlay.ChestElementId,
            InteractiveId = GuildChest.InteractiveId,
            OnCurrentMap = true,
            EnabledSkills = { new InteractiveSkill { SkillId = GuildChest.SkillId, InstanceId = ChestInstanceId } },
        });
        connection.Server(information);
        connection.Server(new InventoryContentEvent { Kamas = 1_000, Objects = { carried } });
        connection.Server(new InventoryWeightEvent { Weight = weight, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    private static TransferActivity Transfer(TestPlay play, TransferSettings settings, TransferBags? bags = null)
    {
        return new TransferActivity(settings, play.Tables, play.Places, bags: bags);
    }

    /// <summary>The banker's question, then the bank opened holding the stacks given, as BankingTests' captures order them.</summary>
    private static async Task OpenBankAsync(TestPlay play, TestConnection connection, int sent, params InventoryObject[] stacks)
    {
        Assert.Equal(_talk, await TestWalks.SentAsync(play, connection, sent));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { new NpcReply { ReplyId = TestPlay.ChestReplyId } } });

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ChestReplyId }, await TestWalks.SentAsync(play, connection, sent + 1));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent { Objects = { stacks } });
    }

    private static async Task OpenChestAsync(TestPlay play, TestConnection connection, int sent, params InventoryObject[] stacks)
    {
        Assert.Equal(new InteractiveUseRequest { ElementId = TestPlay.ChestElementId, SkillInstanceId = ChestInstanceId }, await TestWalks.SentAsync(play, connection, sent));
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.ChestElementId, SkillId = GuildChest.SkillId });
        connection.Server(new ExchangeStartedWithTabbedStorageEvent { Type = ExchangeType.GuildChest });
        connection.Server(new StorageContentEvent { Objects = { stacks } });
    }

    private static async Task LeftAsync(TestPlay play, TestConnection connection, int sent)
    {
        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(play, connection, sent));
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });
    }

    private static string[] Texts(IEnumerable<D3B.Bot.Localization.GameText> texts)
    {
        return [.. texts.Select(text => text.Format.In(CultureInfo.InvariantCulture))];
    }

    [Fact]
    public async Task TheTransferCarriesWhatThePodsHoldFromTheBankToTheChestInAsManyRoundsAsNeededThenSaysWhatItMoved()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var transfer = Transfer(play, new TransferSettings { Categories = [ItemCategory.Resource] });
        var run = bot.StartActivity(transfer);

        // 600 pods free and an ash weighing 5: 120 of the 150 the bank holds, the bread a consumable.
        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 150), Stack(BankBread, TestPlay.Bread, 4));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -120 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 120) });
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(BankAsh, TestPlay.Ash, 30) });
        connection.Server(new InventoryWeightEvent { Weight = 1_000, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 4);

        await OpenChestAsync(play, connection, 5);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 40, Quantity = 120 }, await TestWalks.SentAsync(play, connection, 6));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(900, TestPlay.Ash, 120) });
        connection.Server(new ObjectDeletedEvent { Uid = 40 });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 7);

        await OpenBankAsync(play, connection, 8, Stack(BankAsh, TestPlay.Ash, 30), Stack(BankBread, TestPlay.Bread, 4));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -30 }, await TestWalks.SentAsync(play, connection, 10));
        connection.Server(new ObjectAddedEvent { Object = Stack(41, TestPlay.Ash, 30) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankAsh });
        connection.Server(new InventoryWeightEvent { Weight = 550, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 11);

        await OpenChestAsync(play, connection, 12, Stack(900, TestPlay.Ash, 120));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 41, Quantity = 30 }, await TestWalks.SentAsync(play, connection, 13));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(900, TestPlay.Ash, 150) });
        connection.Server(new ObjectDeletedEvent { Uid = 41 });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 14);

        // The bank holds nothing more to move: the transfer is done once it left.
        await OpenBankAsync(play, connection, 15, Stack(BankBread, TestPlay.Bread, 4));
        await LeftAsync(play, connection, 17);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(17, connection.Sent.Count);
        Assert.Equal("Transfer over: {0} objects of {1} items moved in {2} rounds", transfer.Report!.Doing.Format.In(CultureInfo.InvariantCulture));
        Assert.Equal([TestPlay.Ash], transfer.Report.Counts.Select(count => count.ItemId));
        Assert.Contains(play.Log.GetSnapshot(), record => record.Message.Contains("150 objects of 1 items moved in 2 rounds", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADryRunOpensTheSourceSaysWhatWouldMoveAndMovesNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var transfer = Transfer(play, new TransferSettings { Direction = TransferDirection.GuildChestToBank, Types = [TestPlay.WoodType], Quantity = TransferQuantity.KeepInSource, Amount = 50, DryRun = true });
        var run = bot.StartActivity(transfer);

        await OpenChestAsync(play, connection, 1, Stack(900, TestPlay.Ash, 120), Stack(901, TestPlay.Chestnut, 30), Stack(902, TestPlay.Bread, 9));
        await LeftAsync(play, connection, 2);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(2, connection.Sent.Count);
        Assert.Equal("Dry run: {0} objects of {1} items would move", transfer.Report!.Doing.Format.In(CultureInfo.InvariantCulture));

        // 50 ashes stay in the chest, and the 30 chestnuts are no more than the 50 kept.
        Assert.Equal([TestPlay.Ash], transfer.Report.Counts.Select(count => count.ItemId));
        Assert.Contains(play.Log.GetSnapshot(), record => record.Message.Contains($"would move 70 of item {TestPlay.Ash}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnItemTheChestsTabRefusesGoesBackToTheBankAndIsNotTriedAgain()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var transfer = Transfer(play, new TransferSettings { Items = [TestPlay.Ash, TestPlay.Bread] });
        var run = bot.StartActivity(transfer);

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 10), Stack(BankBread, TestPlay.Bread, 4));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -10 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankAsh });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankBread, Quantity = -4 }, await TestWalks.SentAsync(play, connection, 4));
        connection.Server(new ObjectAddedEvent { Object = Stack(41, TestPlay.Bread, 4) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankBread });
        connection.Server(new InventoryWeightEvent { Weight = 454, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 5);

        await OpenChestAsync(play, connection, 6);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 40, Quantity = 10 }, await TestWalks.SentAsync(play, connection, 7));
        connection.Server(new TextInformationEvent { TextId = StorageMoves.TypeRefusedTextId, Type = TextInformationType.Error });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 41, Quantity = 4 }, await TestWalks.SentAsync(play, connection, 8));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(901, TestPlay.Bread, 4) });
        connection.Server(new ObjectDeletedEvent { Uid = 41 });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 9);

        // The ashes go back first, and are not taken again.
        await OpenBankAsync(play, connection, 10);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 40, Quantity = 10 }, await TestWalks.SentAsync(play, connection, 12));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(BankAsh + 10, TestPlay.Ash, 10) });
        connection.Server(new ObjectDeletedEvent { Uid = 40 });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 13);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(13, connection.Sent.Count);
        Assert.Equal(["{0}: the chest's tab does not take this type"], Texts(transfer.Report!.Warnings));
        Assert.Equal([TestPlay.Bread], transfer.Report.Counts.Select(count => count.ItemId));
    }

    [Fact]
    public async Task AnObjectTheChestRefusesAsBoundIsNotTakenAgainByTheCharactersNextRun()
    {
        // Measured on the owner's run of 2026-11-26 12:57 UTC: a mule's bound object, refused with text 356, was taken out of
        // the bank, refused and put back once a run, four runs in a row, two openings of the bank each time.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var bags = new TransferBags();
        var settings = new TransferSettings { Items = [TestPlay.Ash, TestPlay.Bread] };
        var first = bot.StartActivity(Transfer(play, settings, bags));

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 10), Stack(BankBread, TestPlay.Bread, 4));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -10 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankAsh });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankBread, Quantity = -4 }, await TestWalks.SentAsync(play, connection, 4));
        connection.Server(new ObjectAddedEvent { Object = Stack(41, TestPlay.Bread, 4) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankBread });
        connection.Server(new InventoryWeightEvent { Weight = 454, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 5);

        await OpenChestAsync(play, connection, 6);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 40, Quantity = 10 }, await TestWalks.SentAsync(play, connection, 7));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(900, TestPlay.Ash, 10) });
        connection.Server(new ObjectDeletedEvent { Uid = 40 });
        connection.Server(new InventoryWeightEvent { Weight = 404, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 41, Quantity = 4 }, await TestWalks.SentAsync(play, connection, 8));
        connection.Server(new TextInformationEvent { TextId = GuildChest.RefusedTextId, Type = TextInformationType.Error });
        await LeftAsync(play, connection, 9);

        await OpenBankAsync(play, connection, 10);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 41, Quantity = 4 }, await TestWalks.SentAsync(play, connection, 12));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(BankBread, TestPlay.Bread, 4) });
        connection.Server(new ObjectDeletedEvent { Uid = 41 });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 13);
        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, first.Completion));

        // The next run opens the bank, finds only the bound bread, and is done without taking it.
        var transfer = Transfer(play, settings, bags);
        var next = bot.StartActivity(transfer);

        await OpenBankAsync(play, connection, 14, Stack(BankBread, TestPlay.Bread, 4));
        await LeftAsync(play, connection, 16);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, next.Completion));
        Assert.Equal(16, connection.Sent.Count);
        Assert.Equal(["{0} is bound to the account: left out"], Texts(transfer.Report!.Warnings));
    }

    [Fact]
    public async Task ACharacterTooLoadedToTakeAnythingFailsTheTransferSayingSo()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play, weight: 998);
        var run = bot.StartActivity(Transfer(play, new TransferSettings { Items = [TestPlay.Ash] }));

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 10));
        await LeftAsync(play, connection, 3);

        Assert.Equal(ActivityOutcome.Failed, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.DoesNotContain(connection.Sent, request => request is ExchangeObjectMoveRequest);
        Assert.Equal("The character carries too much to take anything out: free some pods first.", Assert.IsType<ActivityException>(run.Failure).Reason!.Format.In(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ARankWithoutTheRightToTakeOutOfTheChestEndsTheTransfer()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var run = bot.StartActivity(Transfer(play, new TransferSettings { Direction = TransferDirection.GuildChestToBank, Categories = [ItemCategory.Resource] }));

        await OpenChestAsync(play, connection, 1, Stack(900, TestPlay.Ash, 10));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 900, Quantity = -10 }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new TextInformationEvent { TextId = StorageMoves.NoWithdrawRightTextId, Type = TextInformationType.Error });
        await LeftAsync(play, connection, 3);

        Assert.Equal(ActivityOutcome.Failed, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal("The guild's rank has no right to take objects out of the chest.", Assert.IsType<ActivityException>(run.Failure).Reason!.Format.In(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task EverythingMovesEveryItemTheSourceHoldsButThoseLeftOut()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var transfer = Transfer(play, new TransferSettings { Everything = true, ExcludedItems = [TestPlay.Bread] });
        var run = bot.StartActivity(transfer);

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 10), Stack(BankBread, TestPlay.Bread, 4), Stack(502, TestPlay.Chestnut, 2));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -10 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankAsh });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 502, Quantity = -2 }, await TestWalks.SentAsync(play, connection, 4));
        connection.Server(new ObjectAddedEvent { Object = Stack(41, TestPlay.Chestnut, 2) });
        connection.Server(new StorageObjectRemovedEvent { Uid = 502 });
        connection.Server(new InventoryWeightEvent { Weight = 460, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 5);

        run.Stop();
        await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion);
        Assert.DoesNotContain(connection.Sent, request => request is ExchangeObjectMoveRequest { Uid: BankBread });
    }

    [Fact]
    public async Task AStackOfTheCharactersOwnIsNeverPutInWhetherTheInventoryGaveTheTakeAStackOfItsOwnOrJoinedIt()
    {
        // Its own ashes, a stack apart as an object of its own effects would be, and its own chestnuts, which the take joins.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play, 400, Stack(12, TestPlay.Ash, 5), Stack(13, TestPlay.Chestnut, 50));
        var run = bot.StartActivity(Transfer(play, new TransferSettings { Items = [TestPlay.Ash, TestPlay.Chestnut] }));

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 10), Stack(502, TestPlay.Chestnut, 20));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -10 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectAddedEvent { Object = Stack(41, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankAsh });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 502, Quantity = -20 }, await TestWalks.SentAsync(play, connection, 4));
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 13, Quantity = 70 } });
        connection.Server(new StorageObjectRemovedEvent { Uid = 502 });
        connection.Server(new InventoryWeightEvent { Weight = 550, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 5);

        await OpenChestAsync(play, connection, 6);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 41, Quantity = 10 }, await TestWalks.SentAsync(play, connection, 7));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(900, TestPlay.Ash, 10) });
        connection.Server(new ObjectDeletedEvent { Uid = 41 });
        connection.Server(new InventoryWeightEvent { Weight = 500, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 13, Quantity = 20 }, await TestWalks.SentAsync(play, connection, 8));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(901, TestPlay.Chestnut, 20) });
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 13, Quantity = 50 } });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 9);

        await OpenBankAsync(play, connection, 10);
        await LeftAsync(play, connection, 12);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal([(12, 5), (13, 50)], bot.State.Inventory.Values.Select(stack => (stack.Uid, stack.Quantity)));
    }

    [Fact]
    public async Task AnItemRefusedIsKeptInTheBagsOrStopsTheTransferAsTheProfileSays()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var transfer = Transfer(play, new TransferSettings { Items = [TestPlay.Ash], WhenRefused = TransferRefusal.KeepCarried });
        var run = bot.StartActivity(transfer);

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 10));
        await TestWalks.SentAsync(play, connection, 3);
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = BankAsh });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 4);
        await OpenChestAsync(play, connection, 5);
        await TestWalks.SentAsync(play, connection, 6);
        connection.Server(new TextInformationEvent { TextId = GuildChest.RefusedTextId, Type = TextInformationType.Error });
        await LeftAsync(play, connection, 7);

        // Kept: the next round puts nothing back, and the bank holds nothing more to move.
        await OpenBankAsync(play, connection, 8);
        await LeftAsync(play, connection, 10);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal([40], bot.State.Inventory.Keys);
        Assert.Equal(["{0} is bound to the account: left out"], Texts(transfer.Report!.Warnings));

        await using var stopping = new TestPlay();
        var (stoppingConnection, stoppingBot) = await InTheBankAsync(stopping);
        var stopped = stoppingBot.StartActivity(Transfer(stopping, new TransferSettings { Items = [TestPlay.Ash], WhenRefused = TransferRefusal.Stop }));

        await OpenBankAsync(stopping, stoppingConnection, 1, Stack(BankAsh, TestPlay.Ash, 10));
        await TestWalks.SentAsync(stopping, stoppingConnection, 3);
        stoppingConnection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        stoppingConnection.Server(new StorageObjectRemovedEvent { Uid = BankAsh });
        stoppingConnection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        await LeftAsync(stopping, stoppingConnection, 4);
        await OpenChestAsync(stopping, stoppingConnection, 5);
        await TestWalks.SentAsync(stopping, stoppingConnection, 6);
        stoppingConnection.Server(new TextInformationEvent { TextId = StorageMoves.TypeRefusedTextId, Type = TextInformationType.Error });
        await LeftAsync(stopping, stoppingConnection, 7);

        Assert.Equal(ActivityOutcome.Failed, await TestWalks.AdvanceUntilDoneAsync(stopping.Time, stopped.Completion));
        Assert.Equal("The destination refused {0}: the transfer stops, as its settings say.", Assert.IsType<ActivityException>(stopped.Failure).Reason!.Format.In(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ARunStoppedBetweenATakeAndItsPutLeavesTheNextRunToPutWhatItCarriedThenGoOn()
    {
        // Measured on the owner's run of 2026-11-25 15:09 UTC: stopped at the banker with the chest's take in the bags, which
        // filled its pods, the next run read the take as the character's own and failed at once for the pods.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var bags = new TransferBags();
        var settings = new TransferSettings { Items = [TestPlay.Ash] };
        var stopped = bot.StartActivity(Transfer(play, settings, bags));

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 150));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -120 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 120) });
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(BankAsh, TestPlay.Ash, 30) });
        connection.Server(new InventoryWeightEvent { Weight = 1_000, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 4);
        stopped.Stop();
        await TestWalks.AdvanceUntilDoneAsync(play.Time, stopped.Completion);

        // A dry run in between moves nothing, not even what the stopped run carried, and leaves it to the next run.
        var sent = connection.Sent.Count;
        var dry = bot.StartActivity(Transfer(play, new TransferSettings { Items = [TestPlay.Ash], DryRun = true }, bags));

        await OpenBankAsync(play, connection, sent + 1, Stack(BankAsh, TestPlay.Ash, 30));
        await LeftAsync(play, connection, sent + 3);
        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, dry.Completion));

        sent = connection.Sent.Count;
        var transfer = Transfer(play, settings, bags);
        var run = bot.StartActivity(transfer);

        // The ashes carried go in first, then the round goes on with what the bank still holds.
        await OpenChestAsync(play, connection, sent + 1);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 40, Quantity = 120 }, await TestWalks.SentAsync(play, connection, sent + 2));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(900, TestPlay.Ash, 120) });
        connection.Server(new ObjectDeletedEvent { Uid = 40 });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, sent + 3);

        await OpenBankAsync(play, connection, sent + 4);
        await LeftAsync(play, connection, sent + 6);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Contains(play.Log.GetSnapshot(), record => record.Message.Contains("120 objects of 1 items moved in 1 rounds", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AStorageClosedUnderTheTakesIsOpenedAgainNotTakenForASourceWithNothingToMove()
    {
        // Measured on the owner's run of 2026-11-25 15:11 UTC: the chest opened 31 ms after a leaving, and the client's own
        // leaving that follows the bot's closed it before any take; the run ended done, having moved nothing.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var run = bot.StartActivity(Transfer(play, new TransferSettings { Items = [TestPlay.Ash] }));

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 10));
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        await OpenBankAsync(play, connection, 3, Stack(BankAsh, TestPlay.Ash, 10));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -10 }, await TestWalks.SentAsync(play, connection, 5));

        run.Stop();
        await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion);
    }

    [Fact]
    public async Task EmptyingTheBagsFirstPutsWhatTheyHoldInTheBankBeforeTheFirstTakeFromTheChest()
    {
        // The owner's case of 2026-11-25 15:41 UTC: bags too full to take anything, after a reconnection that left a take in them.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play, 998, Stack(12, TestPlay.Ash, 5), Stack(13, TestPlay.Chestnut, 50));
        var transfer = Transfer(play, new TransferSettings { Direction = TransferDirection.GuildChestToBank, Items = [TestPlay.Ash], EmptyBagsFirst = true });
        var run = bot.StartActivity(transfer);

        await OpenBankAsync(play, connection, 1);
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 12, 13 } }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectsDeletedEvent { Uids = { 12, 13 } });
        connection.Server(new StorageObjectsUpdateEvent { Objects = { Stack(BankAsh, TestPlay.Ash, 5), Stack(502, TestPlay.Chestnut, 50) } });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 4);

        await OpenChestAsync(play, connection, 5, Stack(900, TestPlay.Ash, 10));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 900, Quantity = -10 }, await TestWalks.SentAsync(play, connection, 6));
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = 900 });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 7);

        await OpenBankAsync(play, connection, 8, Stack(BankAsh, TestPlay.Ash, 5));
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 40 } }, await TestWalks.SentAsync(play, connection, 10));
        connection.Server(new ObjectsDeletedEvent { Uids = { 40 } });
        connection.Server(new StorageObjectsUpdateEvent { Objects = { Stack(BankAsh, TestPlay.Ash, 15) } });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 11);

        // The bags are emptied once: the next round takes out of the chest, which holds nothing more.
        await OpenChestAsync(play, connection, 12);
        await LeftAsync(play, connection, 13);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(13, connection.Sent.Count);
        Assert.Equal([TestPlay.Ash], transfer.Report!.Counts.Select(count => count.ItemId));
    }

    [Fact]
    public async Task EmptyingTheBagsFirstFromTheBankPutsThemInItThenTakesThemOutForTheChestWithTheRest()
    {
        // Measured on the owner's Huppermage of 2026-11-26 16:50 UTC: the bank emptied into the chest, the character's own harvest,
        // 5,970 pods of its 10,136, stayed in its bags, put in the chest by hand after the run (D575).
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play, 25, Stack(12, TestPlay.Ash, 5));
        var transfer = Transfer(play, new TransferSettings { Everything = true, EmptyBagsFirst = true });
        var run = bot.StartActivity(transfer);

        await OpenBankAsync(play, connection, 1);
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 12 } }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        connection.Server(new StorageObjectsUpdateEvent { Objects = { Stack(12, TestPlay.Ash, 5) } });
        connection.Server(new InventoryWeightEvent { Weight = 0, MaxWeight = 1_000 });
        var deposited = play.Time.GetUtcNow();

        // Still in the bank, which is the source: the round's takes follow, the object keeping its id, now the transfer's, once the
        // server would take another action (D577).
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 12, Quantity = -5 }, await TestWalks.SentAsync(play, connection, 4));
        Assert.True(play.Time.GetUtcNow() - deposited >= BotDelays.Measured.Display);
        connection.Server(new ObjectAddedEvent { Object = Stack(12, TestPlay.Ash, 5) });
        connection.Server(new StorageObjectRemovedEvent { Uid = 12 });
        connection.Server(new InventoryWeightEvent { Weight = 25, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 5);

        await OpenChestAsync(play, connection, 6);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 12, Quantity = 5 }, await TestWalks.SentAsync(play, connection, 7));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(900, TestPlay.Ash, 5) });
        connection.Server(new ObjectDeletedEvent { Uid = 12 });
        connection.Server(new InventoryWeightEvent { Weight = 0, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 8);

        await OpenBankAsync(play, connection, 9);
        await LeftAsync(play, connection, 11);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal([TestPlay.Ash], transfer.Report!.Counts.Select(count => count.ItemId));
    }

    [Fact]
    public async Task TheBankTakesTheStacksCarriedWholeByOneRequestAndAStackJoinedToTheCharactersOwnByItsNumber()
    {
        // Measured on the owner's captures of 20 to 26 November (D577): the client puts a list of stacks in the bank at once.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play, 400, Stack(13, TestPlay.Chestnut, 50));
        var transfer = Transfer(play, new TransferSettings { Direction = TransferDirection.GuildChestToBank, Items = [TestPlay.Ash, TestPlay.Bread, TestPlay.Chestnut] });
        var run = bot.StartActivity(transfer);

        await OpenChestAsync(play, connection, 1, Stack(900, TestPlay.Ash, 10), Stack(901, TestPlay.Bread, 2), Stack(902, TestPlay.Chestnut, 20));
        await TestWalks.SentAsync(play, connection, 2);
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = 900 });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        await TestWalks.SentAsync(play, connection, 3);
        connection.Server(new ObjectAddedEvent { Object = Stack(41, TestPlay.Bread, 2) });
        connection.Server(new StorageObjectRemovedEvent { Uid = 901 });
        connection.Server(new InventoryWeightEvent { Weight = 460, MaxWeight = 1_000 });
        await TestWalks.SentAsync(play, connection, 4);
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 13, Quantity = 70 } });
        connection.Server(new StorageObjectRemovedEvent { Uid = 902 });
        connection.Server(new InventoryWeightEvent { Weight = 560, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 5);

        await OpenBankAsync(play, connection, 6);
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 40, 41 } }, await TestWalks.SentAsync(play, connection, 8));
        connection.Server(new ObjectsDeletedEvent { Uids = { 40, 41 } });
        connection.Server(new StorageObjectsUpdateEvent { Objects = { Stack(BankAsh, TestPlay.Ash, 10), Stack(BankBread, TestPlay.Bread, 2) } });
        connection.Server(new InventoryWeightEvent { Weight = 500, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 13, Quantity = 20 }, await TestWalks.SentAsync(play, connection, 9));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(502, TestPlay.Chestnut, 20) });
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 13, Quantity = 50 } });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 10);

        await OpenChestAsync(play, connection, 11);
        await LeftAsync(play, connection, 12);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        Assert.Equal(3, transfer.Report!.Counts.Length);
        Assert.Equal([(13, 50)], bot.State.Inventory.Values.Select(stack => (stack.Uid, stack.Quantity)));
    }

    [Fact]
    public async Task AStackTheBankLeftOutOfTheListGoesInAloneAfterItAndEachObjectCountsOnce()
    {
        // The bank answers nothing to a stack it does not take (D80): the rest of the list goes in, that one is tried alone.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var run = bot.StartActivity(Transfer(play, new TransferSettings { Direction = TransferDirection.GuildChestToBank, Items = [TestPlay.Ash, TestPlay.Bread] }));

        await OpenChestAsync(play, connection, 1, Stack(900, TestPlay.Ash, 10), Stack(901, TestPlay.Bread, 2));
        await TestWalks.SentAsync(play, connection, 2);
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 10) });
        connection.Server(new StorageObjectRemovedEvent { Uid = 900 });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        await TestWalks.SentAsync(play, connection, 3);
        connection.Server(new ObjectAddedEvent { Object = Stack(41, TestPlay.Bread, 2) });
        connection.Server(new StorageObjectRemovedEvent { Uid = 901 });
        connection.Server(new InventoryWeightEvent { Weight = 460, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 4);

        await OpenBankAsync(play, connection, 5);
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 40, 41 } }, await TestWalks.SentAsync(play, connection, 7));
        connection.Server(new ObjectsDeletedEvent { Uids = { 41 } });
        connection.Server(new StorageObjectsUpdateEvent { Objects = { Stack(BankBread, TestPlay.Bread, 2) } });
        connection.Server(new InventoryWeightEvent { Weight = 450, MaxWeight = 1_000 });
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 40, Quantity = 10 }, await TestWalks.SentAsync(play, connection, 8));
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(BankAsh, TestPlay.Ash, 10) });
        connection.Server(new ObjectDeletedEvent { Uid = 40 });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 9);

        await OpenChestAsync(play, connection, 10);
        await LeftAsync(play, connection, 11);

        Assert.Equal(ActivityOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion));
        var puts = play.Log.GetSnapshot().Where(record => record.Message.Contains(": put ", StringComparison.Ordinal)).Select(record => record.Message).ToList();
        Assert.Equal(2, puts.Count);
        Assert.Contains(puts, put => put.Contains($"put 10 of item {TestPlay.Ash}", StringComparison.Ordinal));
        Assert.Contains(puts, put => put.Contains($"put 2 of item {TestPlay.Bread}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AtMostCountsWhatWasTakenOverEveryRound()
    {
        // 80 pods free: 16 ashes a round, at most 20 in all.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play, weight: 920);
        var run = bot.StartActivity(Transfer(play, new TransferSettings { Items = [TestPlay.Ash], Quantity = TransferQuantity.AtMost, Amount = 20 }));

        await OpenBankAsync(play, connection, 1, Stack(BankAsh, TestPlay.Ash, 100));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -16 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectAddedEvent { Object = Stack(40, TestPlay.Ash, 16) });
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(BankAsh, TestPlay.Ash, 84) });
        connection.Server(new InventoryWeightEvent { Weight = 1_000, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 4);
        await OpenChestAsync(play, connection, 5);
        await TestWalks.SentAsync(play, connection, 6);
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(900, TestPlay.Ash, 16) });
        connection.Server(new ObjectDeletedEvent { Uid = 40 });
        connection.Server(new InventoryWeightEvent { Weight = 920, MaxWeight = 1_000 });
        await LeftAsync(play, connection, 7);

        await OpenBankAsync(play, connection, 8, Stack(BankAsh, TestPlay.Ash, 84));
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = BankAsh, Quantity = -4 }, await TestWalks.SentAsync(play, connection, 10));

        run.Stop();
        await TestWalks.AdvanceUntilDoneAsync(play.Time, run.Completion);
    }
}
