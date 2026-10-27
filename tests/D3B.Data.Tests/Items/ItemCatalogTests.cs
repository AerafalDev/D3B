using D3B.Data.Items;

namespace D3B.Data.Tests.Items;

public sealed class ItemCatalogTests
{
    [Fact]
    public void AnItemAndItsTypeAreFoundByTheirIdentifiers()
    {
        var catalog = TestTables.Items();

        Assert.True(catalog.TryGetItem(303, out var item));
        Assert.Equal((1002, 38, 1, 1), (item.NameId, (int)item.TypeId, (int)item.Level, (int)item.Weight));
        Assert.True(catalog.TryGetType(item.TypeId, out var type));
        Assert.Equal(9, type.SuperTypeId);
        Assert.False(catalog.TryGetItem(304, out _));
    }

    [Fact]
    public void AnItemMayNameATypeTheCatalogLacks()
    {
        var catalog = TestTables.Items();

        Assert.True(catalog.TryGetItem(34_569, out var item));
        Assert.False(catalog.TryGetType(item.TypeId, out _));
    }

    [Fact]
    public void ATableOutOfOrderIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ItemCatalog(new Item[] { new(303, 1, 38, 1, 1), new(289, 1, 34, 1, 1) }, ReadOnlyMemory<ItemType>.Empty));
        Assert.Throws<ArgumentException>(() => new ItemCatalog(ReadOnlyMemory<Item>.Empty, new ItemType[] { new(38, 1, 9, 2), new(38, 2, 9, 2) }));
    }
}
