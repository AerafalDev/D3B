using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Banks;

public sealed class StorageMovesTests
{
    private static InventoryObject Stack(int uid, int itemId, int quantity)
    {
        return new InventoryObject { Item = new ObjectItem { Uid = uid, ItemId = itemId, Quantity = quantity }, Position = InventoryItem.NotWorn };
    }

    private static async Task<(TestConnection Connection, BotActor Bot)> InTheChestAsync(TestPlay play)
    {
        var (connection, bot) = await play.EnterAsync(301, TestPlay.InsideMapId);

        connection.Server(new InventoryContentEvent { Objects = { Stack(12, TestPlay.Ash, 40) } });
        connection.Server(new ExchangeStartedWithTabbedStorageEvent { Type = ExchangeType.GuildChest });
        connection.Server(new StorageContentEvent { Objects = { Stack(500, TestPlay.Ash, 120), Stack(501, TestPlay.Bread, 4) } });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    private static async Task<(TestConnection Connection, BotActor Bot)> InTheBankAsync(TestPlay play)
    {
        var (connection, bot) = await play.EnterAsync(301, TestPlay.InsideMapId);

        connection.Server(new InventoryContentEvent { Objects = { Stack(12, TestPlay.Ash, 40) } });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent { Objects = { Stack(500, TestPlay.Ash, 120), Stack(501, TestPlay.Bread, 4), Stack(502, TestPlay.Chestnut, 9) } });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    [Fact]
    public async Task ATakeAsksTheStorageStackWithANegativeQuantityAndEndsOnceTheStorageAndTheWeightAnswered()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheChestAsync(play);
        var take = bot.RunAsync((context, token) => context.TakeFromStorageAsync(500, 30, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 500, Quantity = -30 }, await TestWalks.SentAsync(play, connection, 1));

        // Measured in the bank and the chest: the inventory's stack, then the storage's, then the weight, in one millisecond.
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 12, Quantity = 70 } });
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(500, TestPlay.Ash, 90) });
        connection.Server(new InventoryWeightEvent { Weight = 350, MaxWeight = 1_000 });

        Assert.Equal(StorageMoveOutcome.Moved, await take);
        Assert.Equal(350, bot.State.Weight);
        Assert.Equal(90, bot.State.Storage!.Stacks[500].Quantity);
    }

    [Fact]
    public async Task AMoveRefusedAsAskedTooSoonIsAskedOnceMoreAMomentLater()
    {
        // Measured on the owner's connections of 2026-11-26 (D577): text 268, a bank action asked less than about 0.55 s after the last.
        await using var play = new TestPlay();
        var (connection, bot) = await InTheChestAsync(play);
        var take = bot.RunAsync((context, token) => context.TakeFromStorageAsync(500, 30, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 500, Quantity = -30 }, await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new TextInformationEvent { TextId = StorageMoves.TooSoonTextId, Type = TextInformationType.Error });

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 500, Quantity = -30 }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new TextInformationEvent { TextId = StorageMoves.TooSoonTextId, Type = TextInformationType.Error });

        Assert.Equal(StorageMoveOutcome.Refused, await take);
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task ATakeThePodsRefuseIsTooHeavyAndATakeOfAStackTheStorageLacksIsSentNot()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheChestAsync(play);
        var take = bot.RunAsync((context, token) => context.TakeFromStorageAsync(501, 4, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 501, Quantity = -4 }, await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new TextInformationEvent { TextId = StorageMoves.TooHeavyTextId, Type = TextInformationType.Error });

        Assert.Equal(StorageMoveOutcome.TooHeavy, await take);
        Assert.Equal(StorageMoveOutcome.Busy, await bot.RunAsync((context, token) => context.TakeFromStorageAsync(999, 1, token), TestContext.Current.CancellationToken));
        Assert.Single(connection.Sent);
    }

    [Theory]
    [InlineData(GuildChest.RefusedTextId, StorageMoveOutcome.NotExchangeable)]
    [InlineData(StorageMoves.NoDepositRightTextId, StorageMoveOutcome.NoRight)]
    [InlineData(StorageMoves.TypeRefusedTextId, StorageMoveOutcome.TypeRefused)]
    [InlineData(1, StorageMoveOutcome.Refused)]
    public async Task APutTheServerRefusesSaysWhy(int textId, StorageMoveOutcome expected)
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheChestAsync(play);
        var put = bot.RunAsync((context, token) => context.PutInStorageAsync(12, 10, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 12, Quantity = 10 }, await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new TextInformationEvent { TextId = textId, Type = TextInformationType.Error });

        Assert.Equal(expected, await put);
    }

    [Fact]
    public async Task APutEndsOnceTheStorageAndTheWeightAnsweredAndOneTheServerLeavesUnansweredIsNoAnswer()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheChestAsync(play);
        var put = bot.RunAsync((context, token) => context.PutInStorageAsync(12, 10, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 12, Quantity = 10 }, await TestWalks.SentAsync(play, connection, 1));

        // Measured in the chest: the storage's stack, then the inventory's, then the weight.
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(500, TestPlay.Ash, 130) });
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 12, Quantity = 30 } });
        connection.Server(new InventoryWeightEvent { Weight = 150, MaxWeight = 1_000 });

        Assert.Equal(StorageMoveOutcome.Moved, await put);
        Assert.Equal(30, bot.State.Inventory[12].Quantity);

        // The bank answers nothing to a stack it does not take (D80).
        var unanswered = bot.RunAsync((context, token) => context.PutInStorageAsync(12, 30, token), TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 2);

        Assert.Equal(StorageMoveOutcome.NoAnswer, await TestWalks.AdvanceUntilDoneAsync(play.Time, unanswered));
    }

    [Fact]
    public async Task AListTakenOutOfTheBankAsksTheStacksItHoldsAndEndsOnceTheyCameInAndTheWeight()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var take = bot.RunAsync((context, token) => context.TakeFromBankAsync([500, 501, 999], token), TestContext.Current.CancellationToken);

        Assert.Equal(new ExchangeTransferToInventoryRequest { Uids = { 500, 501 } }, await TestWalks.SentAsync(play, connection, 1));

        // Measured (D580): the storage's stacks removed, the inventory's added with their ids, then the weight, in one millisecond.
        connection.Server(new StorageObjectsRemovedEvent { Uids = { 500, 501 } });
        connection.Server(new ObjectsAddedEvent { Objects = { Stack(500, TestPlay.Ash, 120), Stack(501, TestPlay.Bread, 4) } });
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });

        Assert.Equal(StorageMoveOutcome.Moved, await take);
        Assert.Equal([502], bot.State.Storage!.Stacks.Keys);
        Assert.Equal([12, 500, 501], bot.State.Inventory.Keys);
        Assert.Equal(400, bot.State.Weight);
    }

    [Fact]
    public async Task AListThePodsCutShortIsTooHeavyWhatFitBeingInTheBags()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var take = bot.RunAsync((context, token) => context.TakeFromBankAsync([501, 502], token), TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);

        // Measured: the stacks that fit whole, then of the next what the pods hold, a new stack in the bags, then text 266.
        connection.Server(new StorageObjectsRemovedEvent { Uids = { 501 } });
        connection.Server(new StorageObjectUpdateEvent { Object = Stack(502, TestPlay.Chestnut, 6) });
        connection.Server(new ObjectsAddedEvent { Objects = { Stack(501, TestPlay.Bread, 4), Stack(900, TestPlay.Chestnut, 3) } });
        connection.Server(new InventoryWeightEvent { Weight = 1_000, MaxWeight = 1_000 });
        connection.Server(new TextInformationEvent { TextId = StorageMoves.IncompleteTextId, Type = TextInformationType.Error });

        Assert.Equal(StorageMoveOutcome.TooHeavy, await take);
        Assert.Equal(6, bot.State.Storage!.Stacks[502].Quantity);
        Assert.Equal(3, bot.State.Inventory[900].Quantity);
    }

    [Fact]
    public async Task AListTheBankRefusesSaysWhyAndTheGuildsChestTakesNone()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var take = bot.RunAsync((context, token) => context.TakeFromBankAsync([500], token), TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        connection.Server(new TextInformationEvent { TextId = StorageMoves.FullTextId, Type = TextInformationType.Error });

        Assert.Equal(StorageMoveOutcome.TooHeavy, await take);

        // Measured: the client moves only a stack at a time in the guild's chest.
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });
        connection.Server(new ExchangeStartedWithTabbedStorageEvent { Type = ExchangeType.GuildChest });
        connection.Server(new StorageContentEvent { Objects = { Stack(600, TestPlay.Ash, 10) } });
        await bot.ProcessedAsync();

        Assert.Equal(StorageMoveOutcome.Busy, await bot.RunAsync((context, token) => context.TakeFromBankAsync([600], token), TestContext.Current.CancellationToken));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AListTheBankFindsTooSoonIsAskedAgainOnceAMomentLater()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var take = bot.RunAsync((context, token) => context.TakeFromBankAsync([500], token), TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        connection.Server(new TextInformationEvent { TextId = StorageMoves.TooSoonTextId, Type = TextInformationType.Error });

        // Measured (D577, D580): the server takes it asked again a moment later.
        Assert.Equal(new ExchangeTransferToInventoryRequest { Uids = { 500 } }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new TextInformationEvent { TextId = StorageMoves.TooSoonTextId, Type = TextInformationType.Error });

        Assert.Equal(StorageMoveOutcome.Refused, await take);
    }
}
