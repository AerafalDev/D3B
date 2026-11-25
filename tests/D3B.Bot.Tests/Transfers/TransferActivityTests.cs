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
    private static async Task<(TestConnection Connection, BotActor Bot)> InTheBankAsync(TestPlay play, int weight = 400)
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
        connection.Server(new InventoryContentEvent { Kamas = 1_000 });
        connection.Server(new InventoryWeightEvent { Weight = weight, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    private static TransferActivity Transfer(TestPlay play, TransferSettings settings)
    {
        return new TransferActivity(settings, play.Tables, play.Places);
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
}
