using System.Globalization;
using System.Text.RegularExpressions;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using D3B.Data.WorldMaps;
using D3B.Scripts.Unity;
using SkiaSharp;

namespace D3B.Scripts.Extract;

/// <summary>Reads the world maps' images of the client, and the worlds they draw, into tiles of JPEG (D308, D369).</summary>
/// <remarks>
/// <para>
/// <c>Content/Picto/Worldmaps/worldmap_assets_.bundle</c> holds 1,250 BC7 textures, each a tile of a world's image at a
/// zoom, named by its number in it. Which tile is which, the bundle does not say: its group's catalog gives each key its
/// address, <c>worldmaps/&lt;world&gt;/&lt;zoom&gt;/&lt;tile&gt;.jpg</c> (<see cref="AddressablesCatalog"/>). The worlds' sizes,
/// origins and zooms are the client's <c>WorldMapsDataRoot</c> (client.md).
/// </para>
/// <para>
/// The extraction fails on anything it did not measure rather than keep a world cut or upside down: a key without an
/// address of that shape, a tile numbered other than its sprite, a zoom the table does not list, a group that is not
/// exactly the grid of 1,024-pixel tiles its size makes, a texture that is not one level of BC7, a pixel that is not
/// opaque, a level whose size is not the table's to within a pixel. Each texture is decoded, turned right side up, and
/// encoded again by SkiaSharp as a JPEG at quality <see cref="JpegQuality"/>, chosen by the owner (D369); nothing is
/// resampled, the client's own zooms being kept.
/// </para>
/// </remarks>
internal static partial class WorldMapsExtractor
{
    // UnityEngine.TextureFormat.BC7.
    private const int Bc7Format = 25;

    // The least alpha of a texel that BC7 decodes opaque.
    private const int OpaqueAlpha = 254;

    // The owner's choice among four encodings, measured on the 1,250 tiles (D369, client.md).
    private const int JpegQuality = 75;

    // The pixels by which a level's size may differ from the table's, which the extraction reports.
    private const int MostDifference = 8;

    // How many tiles are read before the processors decode and encode them, which bounds the memory held at once.
    private const int Batch = 64;

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [BundlePath(client), CatalogPath(client), UnityDataTable.PathOf(client, "worldmaps")];
    }

    public static (WorldMapCatalog Catalog, IReadOnlyList<(string Name, byte[] Jpeg)> Tiles) Read(ClientInstall client)
    {
        var worlds = ReadWorlds(client);
        var addresses = AddressablesCatalog.AddressesByKey(CatalogPath(client));

        using var bundle = new UnityBundle(BundlePath(client));

        var tiles = ReadTiles(bundle, addresses, worlds);

        return Build(worlds, tiles);
    }

    private static string BundlePath(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "Worldmaps", "worldmap_assets_.bundle");
    }

    private static string CatalogPath(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "Worldmaps", "catalog_1.0.bin");
    }

    private static SortedDictionary<int, World> ReadWorlds(ClientInstall client)
    {
        var worlds = new SortedDictionary<int, World>();

        foreach (var record in UnityDataTable.ReadById(client, "worldmaps"))
        {
            var zooms = new SortedSet<int>(Comparer<int>.Create(static (a, b) => b.CompareTo(a)));

            foreach (var zoom in record.SingleArray("zoom"))
            {
                var percent = (int)Math.Round(zoom * 100, MidpointRounding.AwayFromZero);

                if (Math.Abs((percent / 100f) - zoom) > 1e-4 || percent is < 1 or > 100 || !zooms.Add(percent))
                    throw new InvalidDataException($"World map {record.Int32("id")} lists zoom {zoom}, which is not a whole percentage, once each, from 1 to 100.");
            }

            if (zooms.Count is 0)
                throw new InvalidDataException($"World map {record.Int32("id")} lists no zoom.");

            var world = new World(
                record.Int32("id"),
                record.Int32("nameId"),
                record.Int32("origineX"),
                record.Int32("origineY"),
                record.Single("mapWidth"),
                record.Single("mapHeight"),
                record.Single("minScale"),
                record.Single("maxScale"),
                record.Single("startScale"),
                record.Int32("totalWidth"),
                record.Int32("totalHeight"),
                record.Byte("visibleOnMap") is not 0,
                [.. zooms]);

            worlds.Add(world.Id, world);
        }

        return worlds;
    }

    private static SortedDictionary<(int World, int Zoom, int Index), (string Name, byte[] Jpeg, int Width, int Height)> ReadTiles(UnityBundle bundle, IReadOnlyDictionary<string, string> addresses, SortedDictionary<int, World> worlds)
    {
        var textures = new Dictionary<long, UnityFields>();
        var sprites = new Dictionary<long, UnityFields>();
        UnityFields? container = null;

        foreach (var file in bundle.Files)
        {
            var metadata = file.file.Metadata;

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.Texture2D))
                textures.Add(info.PathId, ReadFields(file, info, "Texture2D"));

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.Sprite))
                sprites.Add(info.PathId, ReadFields(file, info, "Sprite"));

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.AssetBundle))
                container = ReadFields(file, info, "AssetBundle");
        }

        if (container is null)
            throw new InvalidDataException($"{Path.GetFileName(bundle.Path)} holds no asset bundle object, so no container.");

        // Each key names a texture and its sprite, which must be as numbered as the texture.
        var pairs = new Dictionary<string, (long Texture, long Sprite)>(StringComparer.Ordinal);

        foreach (var entry in container.Items("m_Container"))
        {
            var key = entry.String("first");
            var asset = entry.Nested("second").Nested("asset");

            if (asset.Int32("m_FileID") is not 0)
                throw new InvalidDataException($"The container's {key} is an asset of another file.");

            var id = asset.Int64("m_PathID");
            var seen = pairs.GetValueOrDefault(key);

            if (textures.ContainsKey(id))
                pairs[key] = (id, seen.Sprite);
            else if (sprites.ContainsKey(id))
                pairs[key] = (seen.Texture, id);
            else
                throw new InvalidDataException($"The container's {key} is neither a texture nor a sprite.");
        }

        if (pairs.Count != textures.Count || pairs.Count != sprites.Count || pairs.Values.Any(pair => pair.Texture is 0 || pair.Sprite is 0))
            throw new InvalidDataException($"The container has {pairs.Count:N0} keys for {textures.Count:N0} textures and {sprites.Count:N0} sprites: not one of each.");

        var located = new SortedDictionary<(int World, int Zoom, int Index), (long Texture, UnityFields Fields)>();

        foreach (var (key, pair) in pairs)
        {
            if (!addresses.TryGetValue(key, out var address) || Address().Match(address) is not { Success: true } match)
                throw new InvalidDataException($"The catalog gives key {key} no address of the shape worldmaps/<world>/<zoom>/<tile>.jpg: {addresses.GetValueOrDefault(key) ?? "none"}.");

            var world = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var zoom = (int)Math.Round(double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 100, MidpointRounding.AwayFromZero);
            var index = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            if (!worlds.TryGetValue(world, out var known) || !known.Zooms.Contains(zoom))
                throw new InvalidDataException($"{address} names a world or a zoom the table of world maps does not list.");

            var texture = textures[pair.Texture];
            var sprite = sprites[pair.Sprite];
            var name = index.ToString(CultureInfo.InvariantCulture);

            if (texture.String("m_Name") != name || sprite.String("m_Name") != name || sprite.Nested("m_RD").Nested("texture").Int64("m_PathID") != pair.Texture)
                throw new InvalidDataException($"{address}: the texture is named {texture.String("m_Name")}, its sprite {sprite.String("m_Name")} or points elsewhere.");

            if (!located.TryAdd((world, zoom, index), (pair.Texture, texture)))
                throw new InvalidDataException($"{address} is the address of two tiles.");
        }

        var done = new SortedDictionary<(int World, int Zoom, int Index), (string Name, byte[] Jpeg, int Width, int Height)>();
        var pending = new List<((int World, int Zoom, int Index) Tile, UnityFields Texture, byte[] Blocks)>(Batch);

        foreach (var (tile, (_, texture)) in located)
        {
            CheckTexture(tile, texture);

            var stream = texture.Nested("m_StreamData");

            pending.Add((tile, texture, bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), checked((int)stream.UInt32("size")))));

            if (pending.Count == Batch)
                Encode(pending, done);
        }

        Encode(pending, done);

        return done;
    }

    private static void CheckTexture((int World, int Zoom, int Index) tile, UnityFields texture)
    {
        var (width, height) = (texture.Int32("m_Width"), texture.Int32("m_Height"));
        var blocks = (long)((width + 3) / 4) * ((height + 3) / 4) * 16;

        if (texture.Int32("m_TextureFormat") != Bc7Format || texture.Int32("m_MipCount") != 1 || texture.Int32("m_ImageCount") != 1
            || width is < 1 or > WorldMapLevel.TileSize || height is < 1 or > WorldMapLevel.TileSize
            || texture.Nested("m_StreamData").UInt32("size") != blocks)
            throw new InvalidDataException($"Tile {tile.Index} of world {tile.World} at {tile.Zoom} % is {width}x{height}, format {texture.Int32("m_TextureFormat")}, {texture.Int32("m_MipCount")} levels, not one level of BC7 in a tile.");
    }

    private static void Encode(List<((int World, int Zoom, int Index) Tile, UnityFields Texture, byte[] Blocks)> pending, SortedDictionary<(int World, int Zoom, int Index), (string Name, byte[] Jpeg, int Width, int Height)> done)
    {
        var encoded = new (string Name, byte[] Jpeg, int Width, int Height)[pending.Count];

        try
        {
            Parallel.For(0, pending.Count, i =>
            {
                var (tile, texture, blocks) = pending[i];
                var (width, height) = (texture.Int32("m_Width"), texture.Int32("m_Height"));

                Bc7.Decompress<ColorRGBA<byte>, byte>(blocks, width, height, out var decoded);

                // Unity's texels start at the bottom row; a picture's rows start at the top.
                var stride = width * 4;
                var pixels = new byte[decoded.Length];

                for (var row = 0; row < height; row++)
                    decoded.AsSpan(row * stride, stride).CopyTo(pixels.AsSpan((height - 1 - row) * stride));

                // The tiles were JPEG files, so opaque: BC7 gives a quarter of the texels 254 for 255 (client.md), no less.
                for (var alpha = 3; alpha < pixels.Length; alpha += 4)
                {
                    if (pixels[alpha] < OpaqueAlpha)
                        throw new InvalidDataException($"Tile {tile.Index} of world {tile.World} at {tile.Zoom} % is not opaque, which a JPEG cannot hold.");
                }

                using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

                encoded[i] = (WorldMapCatalog.TileEntryName(tile.World, tile.Zoom, tile.Index), data.ToArray(), width, height);
            });
        }
        catch (AggregateException exception) when (exception.InnerException is InvalidDataException inner)
        {
            throw inner;
        }

        for (var i = 0; i < pending.Count; i++)
            done.Add(pending[i].Tile, encoded[i]);

        pending.Clear();
    }

    private static (WorldMapCatalog Catalog, IReadOnlyList<(string Name, byte[] Jpeg)> Tiles) Build(SortedDictionary<int, World> worlds, SortedDictionary<(int World, int Zoom, int Index), (string Name, byte[] Jpeg, int Width, int Height)> tiles)
    {
        var rows = new List<D3B.Data.WorldMaps.WorldMap>(worlds.Count);
        var levels = new List<WorldMapLevel>();
        var rounded = 0;
        var most = 0d;

        foreach (var world in worlds.Values)
        {
            var first = levels.Count;

            foreach (var zoom in world.Zooms)
            {
                var scale = zoom / 100d;
                var columns = (int)Math.Ceiling((world.Width * scale) / WorldMapLevel.TileSize);
                var lines = (int)Math.Ceiling((world.Height * scale) / WorldMapLevel.TileSize);

                // The level's own size is what its tiles add up to: its first row across, its first column down.
                var width = 0;
                var height = 0;

                for (var column = 0; column < columns; column++)
                    width += tiles.TryGetValue((world.Id, zoom, column + 1), out var top) ? top.Width : 0;

                for (var line = 0; line < lines; line++)
                    height += tiles.TryGetValue((world.Id, zoom, (line * columns) + 1), out var left) ? left.Height : 0;

                // Measured: a level is the table's size at the zoom to within a few pixels, the client having cut it to a multiple of 4.
                var difference = Math.Max(Math.Abs(width - (world.Width * scale)), Math.Abs(height - (world.Height * scale)));

                if (difference >= MostDifference)
                    throw new InvalidDataException($"World {world.Id} at {zoom} % is {width}x{height} by its tiles, where its table says {world.Width * scale:0.##}x{world.Height * scale:0.##}.");

                if (difference > 1e-6)
                    rounded++;

                most = Math.Max(most, difference);

                var level = new WorldMapLevel(zoom, width, height);
                var count = tiles.Keys.Count(key => key.World == world.Id && key.Zoom == zoom);

                if (level.Columns != columns || level.Rows != lines || count != level.TileCount)
                    throw new InvalidDataException($"World {world.Id} at {zoom} % holds {count} tiles, where its {width}x{height} makes {level.Columns}x{level.Rows}.");

                for (var index = 1; index <= level.TileCount; index++)
                {
                    var (_, _, tileWidth, tileHeight) = level.TileBounds(index);

                    if (!tiles.TryGetValue((world.Id, zoom, index), out var tile) || tile.Width != tileWidth || tile.Height != tileHeight)
                        throw new InvalidDataException($"Tile {index} of world {world.Id} at {zoom} % is missing or not {tileWidth}x{tileHeight}.");
                }

                levels.Add(level);
            }

            rows.Add(new D3B.Data.WorldMaps.WorldMap(world.Id, world.NameId, world.OriginX, world.OriginY, world.MapWidth, world.MapHeight, world.MinScale, world.MaxScale, world.StartScale, world.Width, world.Height, world.Visible, first, world.Zooms.Count));
        }

        if (tiles.Count != levels.Sum(level => level.TileCount))
            throw new InvalidDataException($"The bundle holds {tiles.Count:N0} tiles, where the worlds' zooms make {levels.Sum(level => level.TileCount):N0}.");

        var catalog = new WorldMapCatalog(rows.ToArray(), levels.ToArray());
        var bytes = tiles.Values.Sum(tile => (long)tile.Jpeg.Length);

        Console.WriteLine($"World maps: {rows.Count:N0} worlds, {levels.Count:N0} zooms, {tiles.Count:N0} tiles of JPEG at quality {JpegQuality}, {bytes:N0} bytes; {rounded:N0} zooms whose size differs from the table's, by {most:0.#} pixels at most");

        return (catalog, [.. tiles.Values.Select(tile => (tile.Name, tile.Jpeg))]);
    }

    private static UnityFields ReadFields(AssetsFileInstance file, AssetFileInfo info, string owner)
    {
        var tree = UnityTypeTree.From(file.file.Metadata.TypeTreeTypes[info.TypeIdOrIndex], owner);
        var reader = new UnityReader(UnityBundle.ReadObject(file, info), tree);

        return UnityFields.Read(ref reader, 0, owner);
    }

    [GeneratedRegex(@"^worldmaps/(\d+)/(\d+(?:\.\d+)?)/(\d+)\.jpg$", RegexOptions.CultureInvariant)]
    private static partial Regex Address();

    private sealed record World(int Id, int NameId, int OriginX, int OriginY, float MapWidth, float MapHeight, float MinScale, float MaxScale, float StartScale, int Width, int Height, bool Visible, IReadOnlyList<int> Zooms);
}
