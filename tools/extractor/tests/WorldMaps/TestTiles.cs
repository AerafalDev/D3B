using System.Text;
using D3B.Extractor.Tests.Unity;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.WorldMaps;

/// <summary>Builds by hand the objects the tiles are read from: textures, sprites, the bundle's container, the worlds' records.</summary>
/// <remarks>The shapes are ours, written from the extractor's reader: the fields it reads, laid out as Unity lays them.</remarks>
internal static class TestTiles
{
    /// <summary>The stream file a test texture's texels are in.</summary>
    public const string Stream = "archive:/CAB-0/CAB-0.resS";

    public const int Bc7 = 25;

    /// <summary>The bytes of a BC7 texture of this size: a block of 16 bytes per four by four texels.</summary>
    public static int Blocks(int width, int height)
    {
        return (width + 3) / 4 * ((height + 3) / 4) * 16;
    }

    public static Fields Texture(string name, int width, int height, int format = Bc7, int mips = 1, int images = 1, int? size = null)
    {
        var tree = TypeTree.Build("Texture2D",
        [
            Node(0, "Texture2D", "Base"),
            .. String(1, "m_Name"),
            Node(1, "int", "m_Width", 4),
            Node(1, "int", "m_Height", 4),
            Node(1, "int", "m_TextureFormat", 4),
            Node(1, "int", "m_MipCount", 4),
            Node(1, "int", "m_ImageCount", 4),
            Node(1, "StreamingInfo", "m_StreamData"),
            Node(2, "UInt64", "offset", 8),
            Node(2, "unsigned int", "size", 4),
            .. String(2, "path"),
        ]);
        var bytes = new ObjectBytes();

        bytes.String(name);

        foreach (var value in (ReadOnlySpan<int>)[width, height, format, mips, images])
        {
            bytes.Int32(value);
        }

        bytes.Int64(0);
        bytes.Int32(size ?? Blocks(width, height));
        bytes.String(Stream);

        return Read(tree, bytes);
    }

    public static Fields Sprite(string name, long texture)
    {
        var tree = TypeTree.Build("Sprite",
        [
            Node(0, "Sprite", "Base"),
            .. String(1, "m_Name"),
            Node(1, "SpriteRenderData", "m_RD"),
            Node(2, "PPtr<Texture2D>", "texture"),
            Node(3, "int", "m_FileID", 4),
            Node(3, "SInt64", "m_PathID", 8),
        ]);
        var bytes = new ObjectBytes();

        bytes.String(name);
        bytes.Int32(0);
        bytes.Int64(texture);

        return Read(tree, bytes);
    }

    /// <summary>An asset bundle's object, whose container gives each key its assets: each its file, 0 for the bundle's own, and its identifier.</summary>
    public static Fields Container(params (string Key, int File, long PathId)[] entries)
    {
        var tree = TypeTree.Build("AssetBundle",
        [
            Node(0, "AssetBundle", "Base"),
            Node(1, "map", "m_Container"),
            Array(2),
            Node(3, "int", "size", 4),
            Node(3, "pair", "data"),
            .. String(4, "first"),
            Node(4, "AssetInfo", "second"),
            Node(5, "int", "preloadIndex", 4),
            Node(5, "int", "preloadSize", 4),
            Node(5, "PPtr<Object>", "asset"),
            Node(6, "int", "m_FileID", 4),
            Node(6, "SInt64", "m_PathID", 8),
        ]);
        var bytes = new ObjectBytes();

        bytes.Int32(entries.Length);

        foreach (var (key, file, pathId) in entries)
        {
            bytes.String(key);
            bytes.Int32(0);
            bytes.Int32(1);
            bytes.Int32(file);
            bytes.Int64(pathId);
        }

        return Read(tree, bytes);
    }

    /// <summary>A record of the worlds' table, nameless, of this size in pixels at 100 % and these zooms.</summary>
    public static Fields WorldRecord(int id, int width, int height, params float[] zooms)
    {
        var tree = TypeTree.Build("WorldMap",
        [
            Node(0, "WorldMap", "Base"),
            .. ((string[])["id", "nameId", "origineX", "origineY"]).Select(name => Node(1, "int", name, 4)),
            .. ((string[])["mapWidth", "mapHeight", "minScale", "maxScale", "startScale"]).Select(name => Node(1, "float", name, 4)),
            Node(1, "int", "totalWidth", 4),
            Node(1, "int", "totalHeight", 4),
            Node(1, "bool", "visibleOnMap", 1, align: true),
            .. Vector(1, "zoom", "float", 4),
        ]);
        var bytes = new ObjectBytes();

        foreach (var value in (ReadOnlySpan<int>)[id, 0, 6480, 4944])
        {
            bytes.Int32(value);
        }

        foreach (var value in (ReadOnlySpan<float>)[69.5f, 49.7f, 0.2f, 1, 0.6f])
        {
            bytes.Single(value);
        }

        bytes.Int32(width);
        bytes.Int32(height);
        bytes.Byte(1);
        bytes.Align();
        bytes.Int32(zooms.Length);

        foreach (var zoom in zooms)
        {
            bytes.Single(zoom);
        }

        return Read(tree, bytes);
    }

    /// <summary>Texts in the five languages, none held: the worlds the tests build name none.</summary>
    public static TextFiles NoTexts()
    {
        return TextFiles.From(TextFiles.Languages.Select(language =>
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer, Encoding.UTF8);

            writer.Write(language);
            writer.Write(0);
            writer.Write(0);
            writer.Flush();

            return buffer.ToArray();
        }));
    }

    private static Fields Read(TypeTree tree, ObjectBytes bytes)
    {
        var data = bytes.ToArray();
        var reader = new ObjectReader(data, tree);
        var fields = Fields.Read(ref reader, 0, tree.Name);

        reader.ExpectEnd();

        return fields;
    }
}
