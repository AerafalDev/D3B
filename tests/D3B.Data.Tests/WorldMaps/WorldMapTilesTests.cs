using D3B.Data.Packs;
using D3B.Data.Tests.Packs;
using D3B.Data.WorldMaps;

namespace D3B.Data.Tests.WorldMaps;

public sealed class WorldMapTilesTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void ATileIsReadBackByteForByteFromItsOwnEntry()
    {
        var first = new byte[] { 0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9 };
        var second = new byte[] { 0xFF, 0xD8, 9, 0xFF, 0xD9 };
        var bytes = TestPacks.Write(writer =>
        {
            writer.Add(WorldMapCatalog.TileEntryName(1, 80, 37), first);
            writer.Add(WorldMapCatalog.TileEntryName(24, 100, 3), second);
        });

        using var pack = TestPacks.Open(_directory, bytes);
        var tiles = new WorldMapTiles(pack);

        Assert.Equal(first, tiles.Read(1, 80, 37));
        Assert.Equal(second, tiles.Read(24, 100, 3));
    }

    [Fact]
    public void ATileThePackLacksIsNotFound()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add(WorldMapCatalog.TileEntryName(1, 80, 37), new byte[] { 1 })));
        var tiles = new WorldMapTiles(pack);

        Assert.Throws<KeyNotFoundException>(() => tiles.Read(1, 80, 38));
    }

    [Fact]
    public void ADamagedTileFailsAloneWhenRead()
    {
        var bytes = TestPacks.Write(writer =>
        {
            writer.Add(WorldMapCatalog.TileEntryName(1, 80, 1), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            writer.Add(WorldMapCatalog.TileEntryName(1, 80, 2), new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 });
        });

        using (var intact = TestPacks.Open(_directory, bytes))
        {
            var second = intact.Entry(WorldMapCatalog.TileEntryName(1, 80, 2));

            bytes[second.Offset + (second.CompressedLength / 2)] ^= 0xFF;
        }

        using var pack = TestPacks.Open(_directory, bytes);
        var tiles = new WorldMapTiles(pack);

        Assert.Equal(8, tiles.Read(1, 80, 1).Length);
        Assert.Throws<PackFormatException>(() => tiles.Read(1, 80, 2));
    }

    [Fact]
    public void ATileReaderNeedsAPack()
    {
        Assert.Throws<ArgumentNullException>(() => new WorldMapTiles(null!));
    }
}
