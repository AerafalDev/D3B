using System.Globalization;
using System.Text.RegularExpressions;
using AssetsTools.NET.Extra;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.WorldMaps;

/// <summary>The worlds of the client's map and their tiles: <c>worlds.json</c> and <c>tiles/&lt;world&gt;/&lt;zoom&gt;/&lt;n&gt;.jpg</c>.</summary>
/// <remarks>
/// The tiles are BC7 textures of <c>Content/Picto/Worldmaps/worldmap_assets_.bundle</c>, which names them by keys only:
/// the group's own catalog gives each key its address, <c>worldmaps/&lt;world&gt;/&lt;zoom&gt;/&lt;tile&gt;.jpg</c>
/// (3.7.13.17, <c>main:docs/client.md</c>, "The world's maps"). Every count is checked against the worlds' table.
/// </remarks>
internal static partial class WorldMapTables
{
    private const int Bc7Format = 25;

    // A level's size by its tiles is the table's to within 4 pixels (main:docs/client.md); 8 is a mismatch.
    private const int MostDifference = 8;

    // Tiles are decoded a batch at a time, so that the 970 MB of texels are never all in memory at once.
    private const int Batch = 64;

    /// <summary>Reads the worlds and their tiles from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">The client's world maps are not what the extractor measured.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, DataFolder folder, TextWriter output)
    {
        var area = Path.Combine(client.StreamingAssets, "Content", "Picto", "Worldmaps");
        var catalog = Path.Combine(area, "catalog_1.0.bin");

        if (!File.Exists(catalog))
        {
            throw new InvalidDataException($"The client has no {catalog}.");
        }

        var records = DataTable.ReadById(client, "worldmaps");
        var addresses = AddressablesCatalog.AddressesByKey(File.ReadAllBytes(catalog));

        using var bundle = Bundle.Open(Path.Combine(area, "worldmap_assets_.bundle"));

        var zooms = records.ToDictionary(record => record.Int32("id"), ZoomsOf);
        var tiles = ReadTiles(bundle, addresses, zooms, folder);
        var worlds = records.Select(record => WorldOf(record, texts, zooms[record.Int32("id")], tiles)).OrderBy(world => world.Id).ToList();

        if (tiles.Count != worlds.Sum(world => world.Zooms.Sum(zoom => Columns(zoom.Width) * Columns(zoom.Height))))
        {
            throw new InvalidDataException($"The bundle holds {tiles.Count:N0} tiles, which the worlds' zooms do not cut.");
        }

        folder.Write("worlds.json", Write(worlds));

        output.WriteLine($"World maps: {worlds.Count:N0} worlds, {worlds.Sum(world => world.Zooms.Count):N0} zooms, {tiles.Count:N0} tiles at JPEG quality {TileEncoder.Quality}, {tiles.Values.Sum(tile => (long)tile.Bytes):N0} bytes");
    }

    public static byte[] Write(IEnumerable<WorldMap> worlds)
    {
        return JsonTable.Write(worlds, (json, world) =>
        {
            json.WriteNumber("id", world.Id);
            JsonTable.WriteText(json, "name", world.Name);
            json.WriteNumber("originX", world.OriginX);
            json.WriteNumber("originY", world.OriginY);
            json.WriteNumber("mapWidth", world.MapWidth);
            json.WriteNumber("mapHeight", world.MapHeight);
            json.WriteNumber("minScale", world.MinScale);
            json.WriteNumber("maxScale", world.MaxScale);
            json.WriteNumber("startScale", world.StartScale);
            json.WriteNumber("width", world.Width);
            json.WriteNumber("height", world.Height);
            json.WriteBoolean("visible", world.Visible);
            json.WriteStartArray("zooms");

            foreach (var (percent, width, height) in world.Zooms)
            {
                json.WriteStartObject();
                json.WriteNumber("percent", percent);
                json.WriteNumber("width", width);
                json.WriteNumber("height", height);
                json.WriteNumber("columns", Columns(width));
                json.WriteNumber("rows", Columns(height));
                json.WriteEndObject();
            }

            json.WriteEndArray();
        });
    }

    /// <summary>The path of a tile in the data folder.</summary>
    public static string PathOf(int world, int zoom, int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"tiles/{world}/{zoom}/{index}.jpg");
    }

    /// <summary>How many tiles of <see cref="WorldMap.TileSize"/> cut a side of so many pixels.</summary>
    public static int Columns(int pixels)
    {
        return (pixels + WorldMap.TileSize - 1) / WorldMap.TileSize;
    }

    private static int[] ZoomsOf(Fields record)
    {
        var zooms = new SortedSet<int>(Comparer<int>.Create(static (a, b) => b.CompareTo(a)));

        foreach (var zoom in record.SingleArray("zoom"))
        {
            var percent = (int)Math.Round(zoom * 100, MidpointRounding.AwayFromZero);

            if (Math.Abs((percent / 100f) - zoom) > 1e-4 || percent is < 1 or > 100 || !zooms.Add(percent))
            {
                throw new InvalidDataException($"World map {record.Int32("id")} lists zoom {zoom}, which is not a whole percentage, once each, from 1 to 100.");
            }
        }

        return zooms.Count > 0 ? [.. zooms] : throw new InvalidDataException($"World map {record.Int32("id")} lists no zoom.");
    }

    private static WorldMap WorldOf(Fields record, TextFiles texts, int[] zooms, Dictionary<(int World, int Zoom, int Index), (int Width, int Height, int Bytes)> tiles)
    {
        var id = record.Int32("id");
        var (tableWidth, tableHeight) = (record.Int32("totalWidth"), record.Int32("totalHeight"));
        var levels = new List<(int Percent, int Width, int Height)>();

        foreach (var zoom in zooms)
        {
            var scale = zoom / 100d;
            var columns = (int)Math.Ceiling(tableWidth * scale / WorldMap.TileSize);
            var rows = (int)Math.Ceiling(tableHeight * scale / WorldMap.TileSize);
            var width = Enumerable.Range(0, columns).Sum(column => tiles.TryGetValue((id, zoom, column + 1), out var top) ? top.Width : 0);
            var height = Enumerable.Range(0, rows).Sum(row => tiles.TryGetValue((id, zoom, (row * columns) + 1), out var left) ? left.Height : 0);

            if (Math.Max(Math.Abs(width - (tableWidth * scale)), Math.Abs(height - (tableHeight * scale))) >= MostDifference)
            {
                throw new InvalidDataException($"World {id} at {zoom} % is {width}x{height} by its tiles, where its table says {tableWidth * scale:0.##}x{tableHeight * scale:0.##}.");
            }

            for (var index = 1; index <= columns * rows; index++)
            {
                var (column, row) = ((index - 1) % columns, (index - 1) / columns);
                var expected = (Math.Min(WorldMap.TileSize, width - (column * WorldMap.TileSize)), Math.Min(WorldMap.TileSize, height - (row * WorldMap.TileSize)));

                if (!tiles.TryGetValue((id, zoom, index), out var tile) || (tile.Width, tile.Height) != expected)
                {
                    throw new InvalidDataException($"Tile {index} of world {id} at {zoom} % is missing or not {expected.Item1}x{expected.Item2}.");
                }
            }

            levels.Add((zoom, width, height));
        }

        return new WorldMap
        {
            Id = id,
            Name = texts.Name(record.Int32("nameId")),
            OriginX = record.Int32("origineX"),
            OriginY = record.Int32("origineY"),
            MapWidth = record.Single("mapWidth"),
            MapHeight = record.Single("mapHeight"),
            MinScale = record.Single("minScale"),
            MaxScale = record.Single("maxScale"),
            StartScale = record.Single("startScale"),
            Width = tableWidth,
            Height = tableHeight,
            Visible = record.Boolean("visibleOnMap"),
            Zooms = levels,
        };
    }

    private static Dictionary<(int World, int Zoom, int Index), (int Width, int Height, int Bytes)> ReadTiles(Bundle bundle, IReadOnlyDictionary<string, string> addresses, Dictionary<int, int[]> zooms, DataFolder folder)
    {
        var textures = ById(bundle, AssetClassID.Texture2D);
        var sprites = ById(bundle, AssetClassID.Sprite);
        var containers = bundle.Objects(AssetClassID.AssetBundle).Select(Read).ToList();

        if (containers.Count is not 1)
        {
            throw new InvalidDataException($"{bundle.Path} holds {containers.Count} asset bundle objects, where the extractor reads one container.");
        }

        var container = containers[0];
        var located = new SortedDictionary<(int World, int Zoom, int Index), Fields>();

        foreach (var (key, texture, sprite) in Pairs(container, textures, sprites))
        {
            if (!addresses.TryGetValue(key, out var address) || Address().Match(address) is not { Success: true } match)
            {
                throw new InvalidDataException($"The catalog gives key {key} no address of the shape worldmaps/<world>/<zoom>/<tile>.jpg.");
            }

            var world = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var zoom = (int)Math.Round(double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 100, MidpointRounding.AwayFromZero);
            var index = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            var name = index.ToString(CultureInfo.InvariantCulture);

            if (!zooms.TryGetValue(world, out var known) || !known.Contains(zoom))
            {
                throw new InvalidDataException($"{address} names a world or a zoom the table of world maps does not list.");
            }

            if (textures[texture].String("m_Name") != name || sprites[sprite].String("m_Name") != name || sprites[sprite].Nested("m_RD").Nested("texture").Int64("m_PathID") != texture)
            {
                throw new InvalidDataException($"{address}: its texture or its sprite is not named {name}, or the sprite draws another texture.");
            }

            if (!located.TryAdd((world, zoom, index), textures[texture]))
            {
                throw new InvalidDataException($"{address} is the address of two tiles.");
            }
        }

        var done = new Dictionary<(int World, int Zoom, int Index), (int Width, int Height, int Bytes)>();

        foreach (var batch in located.Chunk(Batch))
        {
            var blocks = batch.Select(tile => Texels(bundle, tile.Key, tile.Value)).ToArray();
            var encoded = new byte[batch.Length][];

            try
            {
                Parallel.For(0, batch.Length, i =>
                {
                    var (world, zoom, index) = batch[i].Key;

                    try
                    {
                        encoded[i] = TileEncoder.Encode(blocks[i], batch[i].Value.Int32("m_Width"), batch[i].Value.Int32("m_Height"));
                    }
                    catch (InvalidDataException exception)
                    {
                        throw new InvalidDataException($"Tile {index} of world {world} at {zoom} %: {exception.Message}", exception);
                    }
                });
            }
            catch (AggregateException exception) when (exception.InnerException is InvalidDataException inner)
            {
                throw inner;
            }

            for (var i = 0; i < batch.Length; i++)
            {
                var (world, zoom, index) = batch[i].Key;

                folder.Write(PathOf(world, zoom, index), encoded[i]);
                done.Add(batch[i].Key, (batch[i].Value.Int32("m_Width"), batch[i].Value.Int32("m_Height"), encoded[i].Length));
            }
        }

        return done;
    }

    private static IEnumerable<(string Key, long Texture, long Sprite)> Pairs(Fields container, Dictionary<long, Fields> textures, Dictionary<long, Fields> sprites)
    {
        var pairs = new Dictionary<string, (long Texture, long Sprite)>(StringComparer.Ordinal);

        foreach (var entry in container.Items("m_Container"))
        {
            var key = entry.String("first");
            var asset = entry.Nested("second").Nested("asset");

            if (asset.Int32("m_FileID") is not 0)
            {
                throw new InvalidDataException($"The container's {key} is an asset of another file.");
            }

            var id = asset.Int64("m_PathID");
            var seen = pairs.GetValueOrDefault(key);

            pairs[key] = textures.ContainsKey(id) ? (id, seen.Sprite)
                : sprites.ContainsKey(id) ? (seen.Texture, id)
                : throw new InvalidDataException($"The container's {key} is neither a texture nor a sprite.");
        }

        if (pairs.Count != textures.Count || pairs.Count != sprites.Count || pairs.Values.Any(pair => pair.Texture is 0 || pair.Sprite is 0))
        {
            throw new InvalidDataException($"The container has {pairs.Count:N0} keys for {textures.Count:N0} textures and {sprites.Count:N0} sprites: not one of each.");
        }

        return pairs.Select(pair => (pair.Key, pair.Value.Texture, pair.Value.Sprite));
    }

    private static byte[] Texels(Bundle bundle, (int World, int Zoom, int Index) tile, Fields texture)
    {
        var (width, height) = (texture.Int32("m_Width"), texture.Int32("m_Height"));
        var stream = texture.Nested("m_StreamData");
        var size = (long)((width + 3) / 4) * ((height + 3) / 4) * 16;

        if (texture.Int32("m_TextureFormat") != Bc7Format || texture.Int32("m_MipCount") != 1 || texture.Int32("m_ImageCount") != 1
            || width is < 1 or > WorldMap.TileSize || height is < 1 or > WorldMap.TileSize || stream.UInt32("size") != size)
        {
            throw new InvalidDataException($"Tile {tile.Index} of world {tile.World} at {tile.Zoom} % is {width}x{height}, format {texture.Int32("m_TextureFormat")}: not one level of BC7 in a tile.");
        }

        return bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), checked((int)size));
    }

    private static Dictionary<long, Fields> ById(Bundle bundle, AssetClassID type)
    {
        var assets = new Dictionary<long, Fields>();

        foreach (var asset in bundle.Objects(type))
        {
            if (!assets.TryAdd(asset.PathId, Read(asset)))
            {
                throw new InvalidDataException($"{bundle.Path} holds two {type} objects of identifier {asset.PathId}.");
            }
        }

        return assets;
    }

    private static Fields Read(SerializedObject asset)
    {
        var reader = new ObjectReader(asset.Data, asset.Tree);

        return Fields.Read(ref reader, 0, asset.Tree.Name);
    }

    [GeneratedRegex(@"^worldmaps/(\d+)/(\d+(?:\.\d+)?)/(\d+)\.jpg$", RegexOptions.CultureInvariant)]
    private static partial Regex Address();
}
