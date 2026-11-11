using System.Text;
using D3B.Extractor.Texts;
using D3B.Extractor.WorldMaps;
using SkiaSharp;

namespace D3B.Extractor.Tests.WorldMaps;

public sealed class WorldMapTablesTests
{
    [Fact]
    public void WorldsAreWrittenAsTheAppReadsThem()
    {
        WorldMap[] worlds =
        [
            new()
            {
                Id = 1, Name = null, OriginX = 6480, OriginY = 4944, MapWidth = 69.5f, MapHeight = 49.7f, MinScale = 0.2f, MaxScale = 1, StartScale = 0.6f,
                Width = 10000, Height = 9664, Visible = true, Zooms = [(100, 10000, 9664), (20, 2000, 1932)],
            },
        ];
        var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "world", "worlds.json"));

        // The app's own tests read the same fixture (src-tauri/src/data/world_maps.rs).
        Assert.Equal(expected, WorldMapTables.Write(worlds));
    }

    [Fact]
    public void ATileIsCutIntoSidesOf1024PixelsAndNamedByItsPlace()
    {
        Assert.Equal(10, WorldMapTables.Columns(10000));
        Assert.Equal(1, WorldMapTables.Columns(1024));
        Assert.Equal(2, WorldMapTables.Columns(1025));
        Assert.Equal("tiles/24/75/3.jpg", WorldMapTables.PathOf(24, 75, 3));
    }

    [Fact]
    public void ATileIsEncodedUprightFromTexelsStoredBottomRowFirst()
    {
        // Sixteen rows: the bottom half, stored first, red; the top half blue. JPEG blurs colours over 8 pixels or so.
        byte[] texels = [.. Pixels(16 * 8, 255, 0, 0), .. Pixels(16 * 8, 0, 0, 255)];

        using var image = SKBitmap.Decode(TileEncoder.Jpeg(texels, 16, 16));

        Assert.True(image.GetPixel(0, 0).Blue > 200);
        Assert.True(image.GetPixel(0, 15).Red > 200);
    }

    [Fact]
    public void ATileThatIsNotOpaqueIsRefused()
    {
        var texels = Pixels(4, 255, 0, 0);

        texels[3] = 128;

        Assert.Throws<InvalidDataException>(() => TileEncoder.Jpeg(texels, 4, 1));
    }

    [Fact]
    public void TheCatalogGivesAKeyTheAddressItsOffsetsPointTo()
    {
        // The prefix at 4, the suffix at 30, a record at 50 pointing to the prefix, then the key's offsets and its string.
        var data = new byte[200];

        Put(data, 0, "worldmaps/24");
        Put(data, 26, "75/1.jpg");
        BitConverter.GetBytes(4).CopyTo(data, 50);
        BitConverter.GetBytes(30).CopyTo(data, 100);
        BitConverter.GetBytes(50).CopyTo(data, 104);
        Put(data, 108, "0123456789abcdef0123456789abcdef");

        var addresses = AddressablesCatalog.AddressesByKey(data);

        Assert.Equal("worldmaps/24/75/1.jpg", addresses["0123456789abcdef0123456789abcdef"]);
    }

    [Fact]
    public void TheInstalledClientsTilesAreExtracted()
    {
        var client = Installed.Client();
        using var temporary = new TemporaryFolder();
        var folder = DataFolder.Create(Path.Combine(temporary.Path, "data"));
        using var output = new StringWriter();

        WorldMapTables.Extract(client, TextFiles.Open(client), folder, output);
        folder.Complete("test", "test");

        Assert.True(File.Exists(Path.Combine(temporary.Path, "data", "worlds.json")));
        Assert.True(File.Exists(Path.Combine(temporary.Path, "data", "tiles", "1", "100", "1.jpg")));
    }

    private static byte[] Pixels(int count, byte red, byte green, byte blue)
    {
        return [.. Enumerable.Repeat<byte[]>([red, green, blue, 255], count).SelectMany(pixel => pixel)];
    }

    private static void Put(byte[] data, int lengthAt, string text)
    {
        BitConverter.GetBytes(text.Length).CopyTo(data, lengthAt);
        Encoding.ASCII.GetBytes(text).CopyTo(data, lengthAt + 4);
    }
}
