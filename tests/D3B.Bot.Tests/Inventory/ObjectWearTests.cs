using D3B.Bot.Characters;
using D3B.Bot.Inventory;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Inventory;

public sealed class ObjectWearTests
{
    private const int UniversalNet = 32_521;
    private const int SmallSoulStone = 9_686;

    private static InventoryObject Stack(int uid, int itemId, int quantity, int position = InventoryItem.NotWorn)
    {
        return new InventoryObject { Item = new ObjectItem { Uid = uid, ItemId = itemId, Quantity = quantity }, Position = position };
    }

    [Fact]
    public async Task AWholeStackWornMovesToTheFightConsumableSlot()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(new InventoryContentEvent { Objects = { Stack(60, UniversalNet, 3) } });
        await bot.ProcessedAsync();

        var wear = bot.RunAsync((context, token) => context.WearObjectAsync(60, InventoryItem.FightConsumable, token), TestContext.Current.CancellationToken);

        // Measured on the client 3.7.14.18 (D565): the whole stack named, the server moves it to 31 within 30 ms.
        Assert.Equal(new ObjectSetPositionRequest { Quantity = 3, Uid = 60, Position = 31 }, await connection.SentAsync(1));
        connection.Server(new ObjectMovementEvent { Uid = 60, Position = 31 });
        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });

        Assert.Equal(ObjectWearOutcome.Worn, await wear);
        Assert.Equal(31, bot.State.Inventory[60].Position);
    }

    [Fact]
    public async Task AStackWornInPlaceOfAnotherSendsTheOtherBack()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(new InventoryContentEvent { Objects = { Stack(60, UniversalNet, 3, 31), Stack(61, SmallSoulStone, 2) } });
        await bot.ProcessedAsync();

        var wear = bot.RunAsync((context, token) => context.WearObjectAsync(61, InventoryItem.FightConsumable, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ObjectSetPositionRequest { Quantity = 2, Uid = 61, Position = 31 }, await connection.SentAsync(1));
        connection.Server(new ObjectMovementEvent { Uid = 60, Position = InventoryItem.NotWorn });
        connection.Server(new ObjectMovementEvent { Uid = 61, Position = 31 });

        Assert.Equal(ObjectWearOutcome.Worn, await wear);
        Assert.Equal((InventoryItem.NotWorn, 31), (bot.State.Inventory[60].Position, bot.State.Inventory[61].Position));
    }

    [Fact]
    public async Task NothingIsSentForAStackNotCarriedOrAlreadyWornThere()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(new InventoryContentEvent { Objects = { Stack(60, UniversalNet, 3, 31) } });
        await bot.ProcessedAsync();

        Assert.Equal(ObjectWearOutcome.NotCarried, await bot.RunAsync((context, token) => context.WearObjectAsync(61, InventoryItem.FightConsumable, token), TestContext.Current.CancellationToken));
        Assert.Equal(ObjectWearOutcome.Worn, await bot.RunAsync((context, token) => context.WearObjectAsync(60, InventoryItem.FightConsumable, token), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(connection.Sent, message => message is ObjectSetPositionRequest);
    }

    [Fact]
    public async Task NothingIsSentWhileTheCharacterIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(new InventoryContentEvent { Objects = { Stack(60, UniversalNet, 1) } });
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        await bot.ProcessedAsync();

        Assert.Equal(ObjectWearOutcome.Busy, await bot.RunAsync((context, token) => context.WearObjectAsync(60, InventoryItem.FightConsumable, token), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(connection.Sent, message => message is ObjectSetPositionRequest);
    }

    [Fact]
    public async Task AWearTheServerLeavesUnansweredSaysSo()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(new InventoryContentEvent { Objects = { Stack(60, UniversalNet, 1) } });
        await bot.ProcessedAsync();

        var wear = bot.RunAsync((context, token) => context.WearObjectAsync(60, InventoryItem.FightConsumable, token), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        await play.AdvanceUntilAsync(() => wear.IsCompleted, TimeSpan.FromSeconds(1));

        Assert.Equal(ObjectWearOutcome.NoAnswer, await wear);
        Assert.Equal(InventoryItem.NotWorn, bot.State.Inventory[60].Position);
    }
}
