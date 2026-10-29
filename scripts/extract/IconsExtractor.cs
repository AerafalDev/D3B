using System.Globalization;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using D3B.Data.Icons;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the icons of the items a harvest names and of the professions, decoded from the client's BC7 textures (D135, D136).</summary>
/// <remarks>
/// <para>
/// <c>Picto/Items</c> and <c>Picto/UI</c> hold one texture and one sprite per icon, named by the icon's identifier, in a
/// bundle per scale. Items' icons are 64 pixels at 1x, professions' at 2x, so that every icon is
/// <see cref="IconTable.Size"/> pixels square. The extractor keeps the icons of the items the sub-areas list as
/// harvestable and of those a skill gathers, Base's included, and every profession's (client.md).
/// </para>
/// <para>
/// A texture is BC7, one level, its texels in the bundle's <c>.resS</c>, bottom row first, as Unity stores them; its
/// sprite's rectangle covers it whole, an items' sprite drawing it through a mesh trimmed to its opaque texels. The
/// extraction fails on anything else, rather than keep an icon cut or upside down.
/// </para>
/// </remarks>
internal static class IconsExtractor
{
    // UnityEngine.TextureFormat.BC7.
    private const int Bc7Format = 25;

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [ItemsBundle(client), JobsBundle(client), UnityDataTable.PathOf(client, "subareas")];
    }

    private static string ItemsBundle(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "Items", "item_assets_1x.bundle");
    }

    private static string JobsBundle(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "UI", "job_assets_2x.bundle");
    }

    public static IconCatalog Read(ClientInstall client, ItemCatalog items, JobCatalog jobs)
    {
        var named = new SortedSet<int>(UnityDataTable.ReadById(client, "subareas").SelectMany(record => record.Int32Array("harvestables")));

        foreach (var skill in jobs.Skills.Span)
        {
            if (skill.GatheredItemId is not Skill.NoItem)
                named.Add(skill.GatheredItemId);
        }

        var itemIcons = new SortedSet<int>();

        foreach (var id in named)
        {
            if (!items.TryGetItem(id, out var item))
                throw new InvalidDataException($"Item {id}, which a harvest names, is no item of the client.");

            itemIcons.Add(item.IconId);
        }

        var jobIcons = new SortedSet<int>();

        foreach (var job in jobs.Jobs.Span)
        {
            if (job.IconId is not Job.NoIcon)
                jobIcons.Add(job.IconId);
        }

        var catalog = new IconCatalog(Decode(ItemsBundle(client), itemIcons), Decode(JobsBundle(client), jobIcons));

        Console.WriteLine($"Icons: {catalog.Items.Count:N0} of the {named.Count:N0} items a harvest names, {catalog.Jobs.Count:N0} of the professions");

        return catalog;
    }

    private static IconTable Decode(string path, SortedSet<int> wanted)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"The client has no icons there: {path} does not exist.", path);

        using var bundle = new UnityBundle(path);
        var textures = new Dictionary<long, (int IconId, byte[] Pixels)>();
        var sprites = new HashSet<int>();

        foreach (var file in bundle.Files)
        {
            var metadata = file.file.Metadata;

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var texture = ReadFields(file, info, "Texture2D");

                if (IconOf(texture.String("m_Name")) is { } iconId && wanted.Contains(iconId))
                    textures.Add(info.PathId, (iconId, DecodeTexture(bundle, texture, iconId)));
            }

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.Sprite))
            {
                var sprite = ReadFields(file, info, "Sprite");

                if (IconOf(sprite.String("m_Name")) is not { } iconId || !wanted.Contains(iconId))
                    continue;

                var data = sprite.Nested("m_RD");

                if (!IsWhole(sprite.Nested("m_Rect"))
                    || !textures.TryGetValue(data.Nested("texture").Int64("m_PathID"), out var texture) || texture.IconId != iconId)
                    throw new InvalidDataException($"{Path.GetFileName(path)}: the sprite of icon {iconId} does not cover its own texture whole.");

                sprites.Add(iconId);
            }
        }

        var found = textures.Values.ToDictionary(texture => texture.IconId, texture => texture.Pixels);
        var missing = wanted.Where(id => !found.ContainsKey(id) || !sprites.Contains(id)).ToList();

        if (missing.Count > 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} lacks the icon, or its sprite, of {string.Join(", ", missing.Take(10))}{(missing.Count > 10 ? ", …" : string.Empty)}.");

        var pixels = new byte[wanted.Count * IconTable.IconLength];
        var index = 0;

        foreach (var id in wanted)
            found[id].CopyTo(pixels, index++ * IconTable.IconLength);

        return new IconTable(wanted.ToArray(), pixels);
    }

    private static UnityFields ReadFields(AssetsFileInstance file, AssetFileInfo info, string owner)
    {
        var tree = UnityTypeTree.From(file.file.Metadata.TypeTreeTypes[info.TypeIdOrIndex], owner);
        var reader = new UnityReader(UnityBundle.ReadObject(file, info), tree);

        return UnityFields.Read(ref reader, 0, owner);
    }

    private static int? IconOf(string name)
    {
        return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private static bool IsWhole(UnityFields rectangle)
    {
        return rectangle.Single("x") == 0 && rectangle.Single("y") == 0
            && rectangle.Single("width") == IconTable.Size && rectangle.Single("height") == IconTable.Size;
    }

    private static byte[] DecodeTexture(UnityBundle bundle, UnityFields texture, int iconId)
    {
        var (width, height) = (texture.Int32("m_Width"), texture.Int32("m_Height"));
        var format = texture.Int32("m_TextureFormat");
        var stream = texture.Nested("m_StreamData");
        var size = checked((int)stream.UInt32("size"));

        // BC7 packs a block of 4 by 4 texels in 16 bytes: one byte a texel.
        if (width != IconTable.Size || height != IconTable.Size || format != Bc7Format || texture.Int32("m_MipCount") != 1 || size != width * height)
            throw new InvalidDataException($"Icon {iconId} is {width}x{height}, format {format}, {texture.Int32("m_MipCount")} levels and {size} bytes, not {IconTable.Size}x{IconTable.Size} BC7 in one level.");

        var blocks = bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), size);

        Bc7.Decompress<ColorRGBA<byte>, byte>(blocks, width, height, out var decoded);

        // Unity's texels start at the bottom row; an icon's rows start at the top (IconTable).
        var stride = width * 4;
        var pixels = new byte[decoded.Length];

        for (var row = 0; row < height; row++)
            decoded.AsSpan(row * stride, stride).CopyTo(pixels.AsSpan((height - 1 - row) * stride));

        return pixels;
    }
}
