using D3B.Extractor.Items;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Tests.Items;

public sealed class ItemTablesTests
{
    private static readonly Text _wheat = new() { Fr = "Blé", En = "Wheat", De = "Weizen", Es = "Trigo", Pt = "Trigo" };
    private static readonly Text _wood = new() { Fr = "Bois", En = "Wood", De = "Holz", Es = "Madera", Pt = "Madeira" };
    private static readonly Item _wheatItem = new() { Id = 289, Name = _wheat, TypeId = 34, Level = 1, IconId = 42011 };
    private static readonly Item _woodItem = new() { Id = 303, Name = _wood, TypeId = 38, Level = 1, IconId = 0 };
    private static readonly ItemType _cereal = new() { Id = 34, Name = new Text { Fr = "Céréale", En = "Cereal", De = "Getreide", Es = "Cereal", Pt = "Cereal" }, CategoryId = 2 };
    private static readonly ItemType _woodType = new() { Id = 38, Name = _wood, CategoryId = 2 };

    [Fact]
    public void ItemsAreWrittenAsTheAppReadsThem()
    {
        // The app's own tests read the same fixtures (src-tauri/src/data/items/).
        Assert.Equal(Fixture("items.json"), ItemTables.WriteItems([_wheatItem, _woodItem]));
    }

    [Fact]
    public void ItemTypesAreWrittenAsTheAppReadsThem()
    {
        Assert.Equal(Fixture("item-types.json"), ItemTables.WriteTypes([_cereal, _woodType]));
    }

    [Fact]
    public void ItemsOfTheClientsTypesAreKept()
    {
        var (items, skipped) = ItemTables.Kept([_wheatItem, _woodItem], [_cereal, _woodType]);

        Assert.Equal([289, 303], items.Select(item => item.Id));
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void AnItemOfATypeTheClientLacksIsLeftOutAndCounted()
    {
        var (items, skipped) = ItemTables.Kept([_wheatItem, _woodItem], [_woodType]);

        Assert.Equal([303], items.Select(item => item.Id));
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void AnItemOrATypeIdHeldTwiceFails()
    {
        var items = Assert.Throws<InvalidDataException>(() => ItemTables.Kept([_woodItem, _woodItem], [_woodType]));
        var types = Assert.Throws<InvalidDataException>(() => ItemTables.Kept([_woodItem], [_woodType, _woodType]));

        Assert.Equal(("The items hold 303 twice.", "The item types hold 38 twice."), (items.Message, types.Message));
    }

    [Fact]
    public void ARecordNamedTextZeroFails()
    {
        Assert.Same(_wood, ItemTables.Named("Item", 303, _wood));

        var failure = Assert.Throws<InvalidDataException>(() => ItemTables.Named("Item type", 264, null));

        Assert.Equal("Item type 264 names text 0.", failure.Message);
    }

    [Fact]
    public void TheInstalledClientsItemsAreKeptButTheOneOfAMissingType()
    {
        var client = Installed.Client("3.7.13.17");
        var texts = TextFiles.Open(client);
        var types = ItemTables.ReadTypes(DataTable.ReadById(client, "itemtypes"), texts);
        var (items, skipped) = ItemTables.Kept(ItemTables.ReadItems(DataTable.ReadById(client, "items"), texts), types);

        // 3.7.13.17, the study of the items of 2026-11-14: 21,622 items, 34,569 of type 264, which the client lacks.
        Assert.Equal((21_621, 1, 236), (items.Count, skipped, types.Count));
        Assert.DoesNotContain(items, item => item.Id == 34_569);
        Assert.Equal(items.Select(item => item.Id).Order(), items.Select(item => item.Id));
    }

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "items", name));
    }
}
