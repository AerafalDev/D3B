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
}
