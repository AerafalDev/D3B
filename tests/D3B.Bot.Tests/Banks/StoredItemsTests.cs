using D3B.Bot.Banks;
using D3B.Data.Items;

namespace D3B.Bot.Tests.Banks;

public sealed class StoredItemsTests
{
    private const int Ore = 312;
    private const int Potion = 10_967;
    private const int QuestObject = 13_052;
    private const int Unknown = 99_999;

    private static readonly ItemCatalog _items = new(
        new[] { new Item(Ore, 1_312, 39, 1, 5, 0), new Item(Potion, 11_267, 69, 1, 0, 0), new Item(QuestObject, 13_352, 148, 1, 0, 0) },
        new[] { new ItemType(39, 1_039, 9, (int)ItemCategory.Resource), new ItemType(69, 1_069, 6, (int)ItemCategory.Consumable), new ItemType(148, 1_148, 14, (int)ItemCategory.Quest) });

    [Fact]
    public void TheStoragesTakeAllButAQuestsObjectsAndAnItemTheTablesLackIsAsked()
    {
        Assert.True(_items.TakesInStorage(Ore));
        Assert.True(_items.TakesInStorage(Potion));
        Assert.False(_items.TakesInStorage(QuestObject));
        Assert.True(_items.TakesInStorage(Unknown));
    }

    [Fact]
    public void ADepositLeavesOutWhatTheStoragesRefuseAndWhatWeighsNothing()
    {
        Assert.True(_items.IsDeposited(Ore));
        Assert.False(_items.IsDeposited(Potion));
        Assert.False(_items.IsDeposited(QuestObject));
        Assert.True(_items.IsDeposited(Unknown));
    }
}
