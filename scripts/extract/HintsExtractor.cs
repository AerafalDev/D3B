using System.Buffers.Binary;
using System.Globalization;
using AssetRipper.TextureDecoder.Dxt;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET.Extra;
using D3B.Data.Hints;
using D3B.Data.Icons;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the hints of the world map, their categories and their pictures.</summary>
/// <remarks>
/// <para>
/// A hint's picture is a texture named by its graphic, in the client's <c>uidarkstone_assets_all</c> addressables bundle
/// (client.md): 64 pixels square in DXT5, streamed, one of them held inline in the object, and the 18 class temples' 62
/// pixels in RGBA32, inline too, which are set in the middle of 64 here. A graphic the bundle lacks is drawn without a picture: the studio shows
/// the hint by its kind alone. The extraction fails when a hint names a category or a text the client lacks, and when the
/// bundle holds no picture at all, so that a client moving them is noticed.
/// </para>
/// </remarks>
internal static class HintsExtractor
{
    private const int Size = 64;
    private const int Rgba32Format = 4;
    private const int Dxt5Format = 12;

    // What follows an inline texture's pixels in the object: the stream data's offset, size and empty path.
    private const int StreamDataLength = 16;

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [UnityDataTable.PathOf(client, "hints"), UnityDataTable.PathOf(client, "hintcategories"), PicturesBundle(client)];
    }

    public static HintCatalog Read(ClientInstall client, IReadOnlySet<int> held)
    {
        var categories = UnityDataTable.ReadById(client, "hintcategories")
            .Select(record => new HintCategory(record.Int32("id"), record.Int32("nameId")))
            .OrderBy(category => category.Id)
            .ToArray();
        var known = categories.Select(category => category.Id).ToHashSet();
        var hints = new List<Hint>();

        foreach (var record in UnityDataTable.ReadById(client, "hints").OrderBy(record => record.Int32("id")))
        {
            var hint = new Hint(
                record.Int32("id"),
                record.Int32("categoryId"),
                record.Int32("gfx"),
                record.Int32("nameId"),
                record.Int32("mapId"),
                record.Int32("worldMapId"),
                record.Int32("x"),
                record.Int32("y"),
                checked((int)record.UInt32("level")));

            if (!known.Contains(hint.CategoryId))
                throw new InvalidDataException($"Hint {hint.Id} names category {hint.CategoryId}, which the client's table lacks.");

            if (!held.Contains(hint.NameId))
                throw new InvalidDataException($"Hint {hint.Id} names text {hint.NameId}, which a language lacks.");

            hints.Add(hint);
        }

        foreach (var category in categories)
        {
            if (!held.Contains(category.NameId))
                throw new InvalidDataException($"Hint category {category.Id} names text {category.NameId}, which a language lacks.");
        }

        if (hints.Count is 0)
            throw new InvalidDataException("The client's table of hints is empty.");

        var wanted = hints.Select(hint => hint.Gfx).ToHashSet();
        var icons = Pictures(client, wanted);
        var lacking = wanted.Where(gfx => !icons.Ids.Span.Contains(gfx)).Order().ToList();

        Console.WriteLine($"Hints: {hints.Count:N0} in {hints.Select(hint => hint.CategoryId).Distinct().Count():N0} of {categories.Length:N0} categories, {icons.Count:N0} pictures of the {wanted.Count:N0} graphics they name; drawn without: {(lacking.Count > 0 ? string.Join(", ", lacking) : "none")}");

        return new HintCatalog(hints.ToArray(), categories, icons);
    }

    private static string PicturesBundle(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "aa", "StandaloneWindows64", "uidarkstone_assets_all.bundle");
    }

    private static IconTable Pictures(ClientInstall client, HashSet<int> wanted)
    {
        var path = PicturesBundle(client);

        if (!File.Exists(path))
            throw new FileNotFoundException($"The client has no hint pictures there: {path} does not exist.", path);

        using var bundle = new UnityBundle(path);
        var found = new SortedDictionary<int, byte[]>();

        foreach (var file in bundle.Files)
        {
            foreach (var info in file.file.Metadata.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var texture = IconsExtractor.ReadFields(file, info, "Texture2D");

                if (!int.TryParse(texture.String("m_Name"), NumberStyles.None, CultureInfo.InvariantCulture, out var gfx) || !wanted.Contains(gfx))
                    continue;

                var picture = Decode(bundle, UnityBundle.ReadObject(file, info), texture, gfx);

                if (!found.TryAdd(gfx, picture))
                    throw new InvalidDataException($"{Path.GetFileName(path)} holds two textures named {gfx}.");
            }
        }

        if (found.Count is 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} holds no picture of a hint: the client keeps them elsewhere.");

        var pixels = new byte[found.Count * Size * Size * 4];
        var at = 0;

        foreach (var picture in found.Values)
        {
            picture.CopyTo(pixels, at);
            at += picture.Length;
        }

        return new IconTable(Size, found.Keys.ToArray(), pixels);
    }

    // Unity's texels start at the bottom row; a picture's rows start at the top, and the 62 pixels of a class temple are set in the middle of the 64.
    private static byte[] Decode(UnityBundle bundle, byte[] bytes, UnityFields texture, int gfx)
    {
        var (width, height) = (texture.Int32("m_Width"), texture.Int32("m_Height"));
        var format = texture.Int32("m_TextureFormat");
        var stream = texture.Nested("m_StreamData");
        var length = format is Dxt5Format ? width * height : width * height * 4;

        if (format is not (Dxt5Format or Rgba32Format) || texture.Int32("m_MipCount") != 1 || width > Size || height > Size)
            throw new InvalidDataException($"Hint picture {gfx} is {width}x{height}, format {format} and {texture.Int32("m_MipCount")} levels: the extractor reads one level of at most {Size} pixels in DXT5 or RGBA32.");

        byte[] texels;

        if (stream.String("path").Length > 0)
        {
            if (checked((int)stream.UInt32("size")) != length)
                throw new InvalidDataException($"Hint picture {gfx} streams {stream.UInt32("size")} bytes, where {width}x{height} in format {format} takes {length}.");

            texels = bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), length);
        }
        else
        {
            var start = bytes.Length - StreamDataLength - length;

            if (start < 4 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(start - 4, 4)) != length)
                throw new InvalidDataException($"Hint picture {gfx}: the inline texels are not where the extractor expects them, {length} bytes before the stream data.");

            texels = bytes.AsSpan(start, length).ToArray();
        }

        byte[] decoded;

        if (format is Dxt5Format)
            DxtDecoder.DecompressDXT5<ColorRGBA<byte>, byte>(texels, width, height, out decoded);
        else
            decoded = texels;

        var picture = new byte[Size * Size * 4];
        var left = (Size - width) / 2;
        var top = (Size - height) / 2;

        for (var row = 0; row < height; row++)
            decoded.AsSpan(row * width * 4, width * 4).CopyTo(picture.AsSpan((((top + (height - 1 - row)) * Size) + left) * 4));

        return picture;
    }
}
