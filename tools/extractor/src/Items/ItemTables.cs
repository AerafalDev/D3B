using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Items;

/// <summary>The items of the data folder and their types: <c>items.json</c> and <c>item-types.json</c>.</summary>
/// <remarks>
/// The client's <c>items</c> table holds <c>ItemData</c> and <c>WeaponData</c> records, read alike for the fields
/// written here; <c>itemtypes</c> holds <c>ItemTypeData</c> (3.7.13.17, <c>artifacts/studies/items.md</c>).
/// </remarks>
internal static class ItemTables
{
    /// <summary>Reads the items and their types from the client and writes them into the folder.</summary>
    /// <returns>The items written, which the skills gather.</returns>
    /// <exception cref="InvalidDataException">An item or a type names text 0, or an id is held twice.</exception>
    public static IReadOnlyList<Item> Extract(ClientInstall client, TextFiles texts, DataFolder folder, TextWriter output)
    {
        var types = ReadTypes(DataTable.ReadById(client, "itemtypes"), texts);
        var (items, skipped) = Kept(ReadItems(DataTable.ReadById(client, "items"), texts), types);

        folder.Write("items.json", WriteItems(items));
        folder.Write("item-types.json", WriteTypes(types));

        output.WriteLine($"Items: {items.Count:N0} items, {types.Count:N0} types; left out {skipped:N0} of a type the client lacks");

        return items;
    }

    /// <exception cref="InvalidDataException">An item names text 0.</exception>
    public static IReadOnlyList<Item> ReadItems(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new Item
                    {
                        Id = id,
                        Name = Named("Item", id, texts.Name(record.UInt32("nameId"))),
                        TypeId = record.UInt16("typeId"),
                        Level = record.Byte("level"),
                        IconId = record.Int32("iconId"),
                    };
                })
                .OrderBy(item => item.Id),
        ];
    }

    /// <exception cref="InvalidDataException">A type names text 0.</exception>
    public static IReadOnlyList<ItemType> ReadTypes(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new ItemType { Id = id, Name = Named("Item type", id, texts.Name(record.Int32("nameId"))), CategoryId = record.Int32("categoryId") };
                })
                .OrderBy(type => type.Id),
        ];
    }

    /// <summary>Leaves out the items of a type the client lacks, and counts them.</summary>
    /// <remarks>Item 34,569 names type 264, which 3.7.13.17 lacks: a test item, left out by the owner's choice of 2026-11-14.</remarks>
    /// <exception cref="InvalidDataException">An item or a type id is held twice, which the app refuses.</exception>
    public static (IReadOnlyList<Item> Items, int Skipped) Kept(IReadOnlyList<Item> items, IReadOnlyList<ItemType> types)
    {
        var known = Unique("item types", types, type => type.Id);

        Unique("items", items, item => item.Id);
        IReadOnlyList<Item> kept = [.. items.Where(item => known.Contains(item.TypeId))];

        return (kept, items.Count - kept.Count);
    }

    /// <summary>Gets the ids of a table's records.</summary>
    /// <exception cref="InvalidDataException">An id is held twice.</exception>
    public static HashSet<int> Unique<T>(string table, IEnumerable<T> records, Func<T, int> id)
    {
        var ids = new HashSet<int>();

        foreach (var record in records)
        {
            if (!ids.Add(id(record)))
            {
                throw new InvalidDataException($"The {table} hold {id(record)} twice.");
            }
        }

        return ids;
    }

    /// <summary>Gets a record's name, which every item, type, job and skill has.</summary>
    /// <exception cref="InvalidDataException">The record names text 0.</exception>
    public static Text Named(string record, int id, Text? name)
    {
        return name ?? throw new InvalidDataException($"{record} {id} names text 0.");
    }

    public static byte[] WriteItems(IEnumerable<Item> items)
    {
        return JsonTable.Write(items, (json, item) =>
        {
            json.WriteNumber("id", item.Id);
            JsonTable.WriteText(json, "name", item.Name);
            json.WriteNumber("typeId", item.TypeId);
            json.WriteNumber("level", item.Level);
            json.WriteNumber("iconId", item.IconId);
        });
    }

    public static byte[] WriteTypes(IEnumerable<ItemType> types)
    {
        return JsonTable.Write(types, (json, type) =>
        {
            json.WriteNumber("id", type.Id);
            JsonTable.WriteText(json, "name", type.Name);
            json.WriteNumber("categoryId", type.CategoryId);
        });
    }
}
