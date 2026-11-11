using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.ExceptionServices;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Maps;

/// <summary>Every map of <c>Content/Map/Data</c>, written in 100 files by the last two digits of its id (ADR 0008).</summary>
/// <remarks>Bundles are read in parallel and the maps sorted by id afterwards, so the files do not depend on the threads.</remarks>
internal static class MapTables
{
    /// <summary>How many files the maps are split in.</summary>
    public const int Shards = 100;

    // A cell's number columns, always written, empty where every cell is zero.
    private static readonly string[] _numbers = ["mapChange", "floor", "moveZone", "arrow", "speed"];

    /// <summary>Reads every map from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">A map is not what the extractor measured, or two share an id.</exception>
    public static IReadOnlyList<MapData> Extract(ClientInstall client, DataFolder folder, TextWriter output)
    {
        var area = Path.Combine(client.StreamingAssets, "Content", "Map", "Data");
        var bundles = Directory.Exists(area) ? Directory.GetFiles(area, "*.bundle").Order(StringComparer.Ordinal).ToArray() : [];

        if (bundles.Length is 0)
        {
            throw new InvalidDataException($"The client has no maps: {area} holds no bundle.");
        }

        var (maps, twoGraphics) = Read(bundles);

        for (var shard = 0; shard < Shards; shard++)
        {
            folder.Write(PathOf(shard), Write(maps.Where(map => map.Id % Shards == shard)));
        }

        output.WriteLine($"Maps: {maps.Count:N0}, {maps.Sum(map => map.Elements.Count):N0} interactive elements, {maps.SelectMany(map => map.Elements).Select(element => element.GfxId).Distinct().Count():N0} graphics; elements drawn twice on their cell with two graphics, kept with the lowest: {twoGraphics:N0}");

        return maps;
    }

    /// <summary>The path of the file that holds the maps whose ids end with <paramref name="shard"/>.</summary>
    public static string PathOf(int shard)
    {
        return string.Create(CultureInfo.InvariantCulture, $"maps/{shard}.json");
    }

    public static byte[] Write(IEnumerable<MapData> maps)
    {
        return JsonTable.Write(maps, (json, map) =>
        {
            json.WriteNumber("id", map.Id);
            json.WriteNumber("top", map.Top);
            json.WriteNumber("bottom", map.Bottom);
            json.WriteNumber("left", map.Left);
            json.WriteNumber("right", map.Right);
            json.WriteStartArray("flags");

            foreach (var flags in map.Flags)
            {
                json.WriteNumberValue(flags);
            }

            json.WriteEndArray();
            json.WriteStartArray("linkedZone");

            foreach (var zone in map.LinkedZone)
            {
                json.WriteNumberValue(zone);
            }

            json.WriteEndArray();

            foreach (var column in _numbers)
            {
                json.WriteStartObject(column);

                foreach (var (cell, value) in map.Numbers.GetValueOrDefault(column) ?? [])
                {
                    json.WriteNumber(cell.ToString(CultureInfo.InvariantCulture), value);
                }

                json.WriteEndObject();
            }

            json.WriteStartArray("elements");

            foreach (var (id, cell, gfx) in map.Elements)
            {
                json.WriteStartObject();
                json.WriteNumber("id", id);
                json.WriteNumber("cellId", cell);
                json.WriteNumber("gfxId", gfx);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        });
    }

    private static (List<MapData> Maps, int TwoGraphics) Read(string[] bundles)
    {
        var maps = new ConcurrentBag<MapData>();
        var twoGraphics = 0;

        try
        {
            Parallel.ForEach(bundles, path =>
            {
                using var bundle = Bundle.Open(path);
                MapReader? reader = null;

                var readers = new List<MapReader>();

                foreach (var map in bundle.MonoBehaviours())
                {
                    if (!MapReader.IsMap(map.Tree))
                    {
                        continue;
                    }

                    // A bundle's serialized files each describe their own reference types.
                    if (reader is null || !ReferenceEquals(reader.References, map.ReferenceTypes))
                    {
                        reader = new MapReader(map.ReferenceTypes);
                        readers.Add(reader);
                    }

                    try
                    {
                        maps.Add(reader.Read(map));
                    }
                    catch (Exception exception) when (exception is InvalidDataException or OverflowException or ArgumentException)
                    {
                        throw new InvalidDataException($"{Path.GetFileName(path)}, object {map.PathId}: {exception.Message}", exception);
                    }
                }

                Interlocked.Add(ref twoGraphics, readers.Sum(each => each.TwoGraphics));
            });
        }
        catch (AggregateException exception)
        {
            var inner = exception.InnerExceptions[0];

            if (inner is InvalidDataException or IOException)
            {
                ExceptionDispatchInfo.Throw(inner);
            }

            throw new InvalidDataException($"The maps cannot be read: {inner.Message}", inner);
        }

        var sorted = maps.OrderBy(map => map.Id).ToList();

        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Id == sorted[i - 1].Id)
            {
                throw new InvalidDataException($"The client holds map {sorted[i].Id} twice.");
            }
        }

        return (sorted, twoGraphics);
    }
}
