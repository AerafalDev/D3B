using D3B.Data.Items;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the items and their types.</summary>
/// <remarks>
/// An item keeps what a bot names, sorts and weighs it by. Its effects, recipes, criteria and <c>m_flags</c>, whose bits
/// are not measured, are left out (client.md).
/// </remarks>
internal static class ItemsExtractor
{
    private static readonly string[] _tables = ["items", "itemtypes"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static ItemCatalog Read(ClientInstall client)
    {
        try
        {
            var items = UnityDataTable.ReadById(client, "items").Select(record => new Item(
                record.Int32("id"),
                checked((int)record.UInt32("nameId")),
                record.UInt16("typeId"),
                record.Byte("level"),
                record.UInt16("realWeight")));
            var types = UnityDataTable.ReadById(client, "itemtypes").Select(record => new ItemType(
                record.Int32("id"),
                record.Int32("nameId"),
                record.Int32("superTypeId"),
                record.Int32("categoryId")));
            var catalog = new ItemCatalog(items.OrderBy(item => item.Id).ToArray(), types.OrderBy(type => type.Id).ToArray());
            var untyped = 0;

            // A test item of client 3.7.12.12 names a type the client lacks; JobsExtractor fails when a resource does.
            foreach (var item in catalog.Items.Span)
            {
                if (!catalog.TryGetType(item.TypeId, out _))
                    untyped++;
            }

            Console.WriteLine($"Items: {catalog.Items.Length:N0}, {catalog.Types.Length:N0} types; {untyped:N0} of a type the client lacks");

            return catalog;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The items' tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }
}
