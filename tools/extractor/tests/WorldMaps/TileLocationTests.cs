using D3B.Extractor.Unity;
using D3B.Extractor.WorldMaps;
using static D3B.Extractor.Tests.WorldMaps.TestTiles;

namespace D3B.Extractor.Tests.WorldMaps;

/// <summary>How the tiles of the bundle are placed and their texels sized, on objects built by hand, so that CI covers the checks without a client.</summary>
public sealed class TileLocationTests
{
    private static readonly Dictionary<int, int[]> _zooms = new() { [1] = [100, 50] };

    [Fact]
    public void ATileIsPlacedByTheAddressOfItsKey()
    {
        var textures = Textures();
        var located = Locate(textures: textures, addresses: new() { ["a"] = "worldmaps/1/0.5/1.jpg", ["b"] = "worldmaps/1/1/2.jpg" });

        Assert.Equal([(1, 50, 1), (1, 100, 2)], located.Keys);
        Assert.Same(textures[11], located[(1, 100, 2)]);
    }

    [Fact]
    public void AnAssetOfAnotherFileIsRefused()
    {
        Refused("is an asset of another file", container: [("a", 1, 10), ("a", 0, 20), ("b", 0, 21), ("b", 0, 11)]);
    }

    [Fact]
    public void AnAssetNeitherTextureNorSpriteIsRefused()
    {
        Refused("is neither a texture nor a sprite", container: [("a", 0, 10), ("a", 0, 20), ("b", 0, 21), ("b", 0, 99)]);
    }

    [Fact]
    public void AKeyWithoutATextureAndASpriteOrATextureWithoutKeyIsRefused()
    {
        Refused("not one of each", container: [("a", 0, 10), ("a", 0, 20), ("b", 0, 21)]);
        Refused("not one of each", textures: new(Textures()) { [12] = Texture("3", 1024, 1024) });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("worldmaps/1/0.5/2.png")]
    [InlineData("maps/1/0.5/2.jpg")]
    [InlineData("worldmaps/1/0.5/b.jpg")]
    public void AKeyWithoutTheAddressOfATileIsRefused(string? address)
    {
        Dictionary<string, string> addresses = new() { ["a"] = "worldmaps/1/0.5/1.jpg" };

        if (address is not null)
        {
            addresses["b"] = address;
        }

        Refused("gives key b no address of the shape", addresses: addresses);
    }

    [Theory]
    [InlineData("worldmaps/2/0.5/2.jpg")]
    [InlineData("worldmaps/1/0.25/2.jpg")]
    public void AnAddressOfAWorldOrAZoomTheTableLacksIsRefused(string address)
    {
        Refused("names a world or a zoom the table of world maps does not list", addresses: new() { ["a"] = "worldmaps/1/0.5/1.jpg", ["b"] = address });
    }

    [Theory]
    [InlineData("3", "2", 11L)]
    [InlineData("2", "3", 11L)]
    [InlineData("2", "2", 10L)]
    public void ATextureOrASpriteOfAnotherNameOrASpriteOfAnotherTextureIsRefused(string texture, string sprite, long drawn)
    {
        Refused("is not named 2, or the sprite draws another texture",
            textures: new(Textures()) { [11] = Texture(texture, 976, 1024) },
            sprites: new(Sprites()) { [21] = Sprite(sprite, drawn) });
    }

    [Fact]
    public void TwoTilesOfOneAddressAreRefused()
    {
        Refused("is the address of two tiles", addresses: new() { ["a"] = "worldmaps/1/0.5/2.jpg", ["b"] = "worldmaps/1/0.5/2.jpg" },
            textures: new(Textures()) { [10] = Texture("2", 1024, 1024) },
            sprites: new(Sprites()) { [20] = Sprite("2", 10) });
    }

    [Theory]
    [InlineData(1024, 1024, 1024 * 1024)]
    [InlineData(1000, 600, 250 * 150 * 16)]
    [InlineData(5, 5, 4 * 16)]
    [InlineData(1, 1, 16)]
    public void ATilesTexelsAreABlockOf16BytesPerFourByFourTexels(int width, int height, int bytes)
    {
        Assert.Equal(bytes, WorldMapTables.TexelBytes((1, 100, 1), Texture("1", width, height)));
    }

    [Theory]
    [InlineData(1024, 1024, 4, 1, 1, 0)]
    [InlineData(1024, 1024, Bc7, 2, 1, 0)]
    [InlineData(1024, 1024, Bc7, 1, 2, 0)]
    [InlineData(0, 1024, Bc7, 1, 1, 0)]
    [InlineData(1025, 1024, Bc7, 1, 1, 0)]
    [InlineData(1024, 0, Bc7, 1, 1, 0)]
    [InlineData(1024, 1025, Bc7, 1, 1, 0)]
    [InlineData(1024, 1024, Bc7, 1, 1, 16)]
    public void ATextureThatIsNotOneLevelOfBc7WithinATileIsRefused(int width, int height, int format, int mips, int images, int extra)
    {
        var texture = Texture("1", width, height, format, mips, images, Blocks(width, height) + extra);
        var error = Assert.Throws<InvalidDataException>(() => WorldMapTables.TexelBytes((1, 100, 1), texture));

        Assert.Contains("Tile 1 of world 1 at 100 %", error.Message, StringComparison.Ordinal);
        Assert.Contains("not one level of BC7 in a tile", error.Message, StringComparison.Ordinal);
    }

    private static Dictionary<long, Fields> Textures()
    {
        return new() { [10] = Texture("1", 1024, 1024), [11] = Texture("2", 976, 1024) };
    }

    private static Dictionary<long, Fields> Sprites()
    {
        return new() { [20] = Sprite("1", 10), [21] = Sprite("2", 11) };
    }

    private static SortedDictionary<(int World, int Zoom, int Index), Fields> Locate(
        (string Key, int File, long PathId)[]? container = null,
        Dictionary<long, Fields>? textures = null,
        Dictionary<long, Fields>? sprites = null,
        Dictionary<string, string>? addresses = null)
    {
        return WorldMapTables.Locate(
            Container(container ?? [("a", 0, 10), ("a", 0, 20), ("b", 0, 21), ("b", 0, 11)]),
            textures ?? Textures(),
            sprites ?? Sprites(),
            addresses ?? new() { ["a"] = "worldmaps/1/0.5/1.jpg", ["b"] = "worldmaps/1/0.5/2.jpg" },
            _zooms);
    }

    private static void Refused(
        string reason,
        (string Key, int File, long PathId)[]? container = null,
        Dictionary<long, Fields>? textures = null,
        Dictionary<long, Fields>? sprites = null,
        Dictionary<string, string>? addresses = null)
    {
        var error = Assert.Throws<InvalidDataException>(() => Locate(container, textures, sprites, addresses));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }
}
