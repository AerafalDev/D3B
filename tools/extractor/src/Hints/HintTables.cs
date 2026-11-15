using System.Globalization;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using D3B.Extractor.World;

namespace D3B.Extractor.Hints;

/// <summary>The world map's hints of the data folder: <c>hints.json</c>, <c>hint-categories.json</c> and their pictures, <c>icons/hints/&lt;gfx&gt;.png</c>.</summary>
/// <remarks>
/// 835 hints in 6 of 12 categories on 3.7.13.17, each on a map of the positions, named in every language; 91 of the 92
/// pictures they name are in the client, 475's is not: its hint keeps its <c>gfx</c>, is counted, and the app draws it
/// without one (<c>main:docs/client.md</c>, "Hints"). <c>realMapId</c>, set on 144 hints and in the table for 8, is not read.
/// </remarks>
internal static class HintTables
{
    /// <summary>Reads the hints, their categories and their pictures from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">The client's tables or pictures are not what the extractor measured.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, IReadOnlyList<MapPosition> positions, DataFolder folder, TextWriter output)
    {
        var categories = ReadCategories(DataTable.ReadById(client, "hintcategories"), texts);
        var hints = ReadHints(DataTable.ReadById(client, "hints"), texts);

        Check(hints, categories, positions);

        var graphics = hints.Select(hint => hint.Gfx).ToHashSet();
        var pictures = HintPictures.Read(client, graphics);

        folder.Write("hints.json", WriteHints(hints));
        folder.Write("hint-categories.json", WriteCategories(categories));

        foreach (var (gfx, png) in pictures)
        {
            folder.Write(PathOf(gfx), png);
        }

        var missing = hints.Count(hint => !pictures.ContainsKey(hint.Gfx));
        var bytes = pictures.Values.Sum(png => (long)png.Length);

        output.WriteLine($"Hints: {hints.Count:N0} in {categories.Count:N0} categories, {pictures.Count:N0} pictures, {bytes:N0} bytes; {missing:N0} hints name a picture the client lacks");
    }

    /// <exception cref="InvalidDataException">A category has no name.</exception>
    public static IReadOnlyList<HintCategory> ReadCategories(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record => new HintCategory
                {
                    Id = record.Int32("id"),
                    Name = texts.Name(record.Int32("nameId")) ?? throw new InvalidDataException($"Hint category {record.Int32("id")} has no name."),
                })
                .OrderBy(category => category.Id),
        ];
    }

    /// <exception cref="InvalidDataException">A hint has no name, or is outdoor by a value other than 0 or 1.</exception>
    public static IReadOnlyList<Hint> ReadHints(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new Hint
                    {
                        Id = id,
                        CategoryId = record.Int32("categoryId"),
                        Gfx = record.Int32("gfx"),
                        Name = texts.Name(record.Int32("nameId")) ?? throw new InvalidDataException($"Hint {id} has no name."),
                        MapId = record.Int32("mapId"),
                        X = record.Int32("x"),
                        Y = record.Int32("y"),
                        Outdoor = record.Byte("outdoor") switch
                        {
                            0 => false,
                            1 => true,
                            var other => throw new InvalidDataException($"Hint {id} is outdoor by {other}, not 0 or 1."),
                        },
                        SubAreaId = record.Int32("subareaId"),
                        WorldMapId = record.Int32("worldMapId"),
                        Level = record.UInt32("level"),
                    };
                })
                .OrderBy(hint => hint.Id),
        ];
    }

    /// <summary>Checks that each hint is of a category and on a map of the positions.</summary>
    /// <exception cref="InvalidDataException">A hint is of no category, or on no map.</exception>
    public static void Check(IReadOnlyList<Hint> hints, IReadOnlyList<HintCategory> categories, IReadOnlyList<MapPosition> positions)
    {
        var known = categories.Select(category => category.Id).ToHashSet();
        var maps = positions.Select(position => (long)position.Id).ToHashSet();

        foreach (var hint in hints)
        {
            if (!known.Contains(hint.CategoryId))
            {
                throw new InvalidDataException($"Hint {hint.Id} is of category {hint.CategoryId}, which the client lacks.");
            }

            if (!maps.Contains(hint.MapId))
            {
                throw new InvalidDataException($"Hint {hint.Id} stands on map {hint.MapId}, which has no position.");
            }
        }
    }

    public static byte[] WriteHints(IEnumerable<Hint> hints)
    {
        return JsonTable.Write(hints, (json, hint) =>
        {
            json.WriteNumber("id", hint.Id);
            json.WriteNumber("categoryId", hint.CategoryId);
            json.WriteNumber("gfx", hint.Gfx);
            JsonTable.WriteText(json, "name", hint.Name);
            json.WriteNumber("mapId", hint.MapId);
            json.WriteNumber("x", hint.X);
            json.WriteNumber("y", hint.Y);
            json.WriteBoolean("outdoor", hint.Outdoor);
            json.WriteNumber("subAreaId", hint.SubAreaId);
            json.WriteNumber("worldMapId", hint.WorldMapId);
            json.WriteNumber("level", hint.Level);
        });
    }

    public static byte[] WriteCategories(IEnumerable<HintCategory> categories)
    {
        return JsonTable.Write(categories, (json, category) =>
        {
            json.WriteNumber("id", category.Id);
            JsonTable.WriteText(json, "name", category.Name);
        });
    }

    /// <summary>The path of a hint's picture in the data folder.</summary>
    public static string PathOf(int gfx)
    {
        return string.Create(CultureInfo.InvariantCulture, $"icons/hints/{gfx}.png");
    }
}
