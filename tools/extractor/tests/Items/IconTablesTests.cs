using AssetsTools.NET.Extra;
using D3B.Extractor.Items;
using D3B.Extractor.Tests.Breeds;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using SkiaSharp;

namespace D3B.Extractor.Tests.Items;

public sealed class IconTablesTests
{
    private const int Rgba32 = 4;
    private const int Bc7 = 25;

    private static readonly Text _name = new() { Fr = "Bois", En = "Wood", De = "Holz", Es = "Madera", Pt = "Madeira" };

    [Fact]
    public void AnIconIsNamedByItsKindAndNumber()
    {
        Assert.Equal("icons/items/0.png", IconTables.PathOf("items", 0));
        Assert.Equal("icons/jobs/42.png", IconTables.PathOf("jobs", 42));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("25257", 25_257)]
    public void AnIconsNameIsItsNumber(string name, int icon)
    {
        Assert.Equal(icon, IconPictures.IconOf(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("01")]
    [InlineData("-1")]
    [InlineData(" 1")]
    [InlineData("1.png")]
    [InlineData("99999999999")]
    public void ANameOfAnotherShapeIsNoIcon(string name)
    {
        Assert.Null(IconPictures.IconOf(name));
    }

    [Fact]
    public void ABc7IconOf64PxIsHalvedUprightWithItsTransparency()
    {
        // Sixteen rows of sixteen blocks, the bottom row first: transparent below, opaque red above.
        var transparent = BreedTablesTests.Bc7Block(0, 0, 0, 0);
        var red = BreedTablesTests.Bc7Block(255, 0, 0, 255);
        byte[] blocks = [.. Enumerable.Range(0, 256).SelectMany(block => block < 128 ? transparent : red)];
        using var bitmap = SKBitmap.Decode(IconPictures.Png(blocks, Bc7, 64));

        Assert.Equal((32, 32), (bitmap.Width, bitmap.Height));
        var top = bitmap.GetPixel(0, 0);

        Assert.True(top.Red > 250 && top.Green is 0 && top.Alpha > 250, $"The top row is {top}, not opaque red.");
        Assert.Equal(0, bitmap.GetPixel(31, 31).Alpha);
    }

    [Fact]
    public void ATexelHalfTransparentKeepsItsColour()
    {
        // Columns of opaque red and of transparent green: averaged without their alpha, the icon would turn olive.
        var texels = Rgba(64, (x, _) => x % 2 == 0 ? (255, 0, 0, 255) : (0, 255, 0, 0));
        using var bitmap = SKBitmap.Decode(IconPictures.Png(texels, Rgba32, 64));
        var pixel = bitmap.GetPixel(10, 10);

        Assert.True(pixel.Red > 250 && pixel.Green < 5 && pixel.Alpha is 127 or 128, $"A half transparent pixel is {pixel}, not red at half alpha.");
    }

    [Fact]
    public void AnIconOf60PxWeighsEachTexelByThePartItCovers()
    {
        // The first icon column covers 1.875 texels: all of the blue first, 0.875 of the red second.
        var texels = Rgba(60, (x, _) => x == 0 ? (0, 0, 255, 255) : (255, 0, 0, 255));
        using var bitmap = SKBitmap.Decode(IconPictures.Png(texels, Rgba32, 60));

        Assert.Equal(new SKColor(119, 0, 136, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(1, 0));
    }

    [Fact]
    public void AnIconOf32PxIsKeptAsItIs()
    {
        var texels = Rgba(32, (x, y) => (x * 8, y * 8, 100, 255));
        using var bitmap = SKBitmap.Decode(IconPictures.Png(texels, Rgba32, 32));

        // Unity's bottom row is the PNG's last.
        Assert.Equal(new SKColor(40, 248, 100, 255), bitmap.GetPixel(5, 0));
        Assert.Equal(new SKColor(40, 0, 100, 255), bitmap.GetPixel(5, 31));
    }

    [Fact]
    public void AnIconOf60PxIsScaledTo32()
    {
        // 60 px is 15 blocks a side, as two icons of 3.7.13.17.
        var red = BreedTablesTests.Bc7Block(255, 0, 0, 255);
        using var bitmap = SKBitmap.Decode(IconPictures.Png([.. Enumerable.Repeat(red, 225).SelectMany(block => block)], Bc7, 60));

        Assert.Equal((32, 32), (bitmap.Width, bitmap.Height));
        Assert.True(bitmap.GetPixel(31, 31).Alpha > 250);
    }

    [Theory]
    [InlineData(12, 64, 4096)]
    [InlineData(Bc7, 16, 256)]
    [InlineData(Bc7, 64, 4095)]
    [InlineData(Rgba32, 32, 4095)]
    [InlineData(Bc7, 128, 16_384)]
    public void ATextureOfAnotherFormatSizeOrLengthFails(int format, int side, int length)
    {
        var failure = Assert.Throws<InvalidDataException>(() => IconPictures.Png(new byte[length], format, side));

        Assert.Equal($"An icon of {side} px in format {format} holds {length} bytes: not BC7 or RGBA32 of 32 to 64 px.", failure.Message);
    }

    [Theory]
    [InlineData(Rgba32, 62, 62, 64)]
    [InlineData(Bc7, 32, 32, 32)]
    public void ASquareLevelOfAKnownFormatAndSidePasses(int format, int width, int height, int largest)
    {
        IconPictures.CheckShape("1", format, width, height, 1, 1, largest, [Bc7, Rgba32]);
    }

    [Theory]
    [InlineData(Bc7, 64, 60, 1, 1, 64)]
    [InlineData(Bc7, 64, 64, 2, 1, 64)]
    [InlineData(Bc7, 64, 64, 1, 6, 64)]
    [InlineData(Bc7, 28, 28, 1, 1, 64)]
    [InlineData(Bc7, 128, 128, 1, 1, 64)]
    [InlineData(Bc7, 64, 64, 1, 1, 32)]
    [InlineData(Rgba32, 32, 32, 1, 1, 32)]
    public void ATextureOfAnotherShapeOrFormatFails(int format, int width, int height, int levels, int images, int largest)
    {
        var failure = Assert.Throws<InvalidDataException>(() => IconPictures.CheckShape("1", format, width, height, levels, images, largest, largest == 32 ? [Bc7] : [Bc7, Rgba32]));

        Assert.StartsWith($"The icon 1 is {width}x{height} in format {format}, {levels} levels, {images} images: not one square level of 32 to {largest} px", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATextureNamedByNoNumberFails()
    {
        var failure = Assert.Throws<InvalidDataException>(() => IconPictures.Add([], "items.bundle", "icon_7", [1]));

        Assert.Equal("items.bundle holds the texture icon_7, which is no icon's number.", failure.Message);
    }

    [Fact]
    public void TwoTexturesOfOneNameFail()
    {
        var icons = new SortedDictionary<int, byte[]>();

        IconPictures.Add(icons, "items.bundle", "7", [1]);
        var failure = Assert.Throws<InvalidDataException>(() => IconPictures.Add(icons, "items.bundle", "7", [2]));

        Assert.Equal("items.bundle holds two textures named 7.", failure.Message);
        Assert.Equal([1], icons[7]);
    }

    [Fact]
    public void TheItemsIconsAreKeptOnceAndTheMissingOnesCounted()
    {
        var icons = new Dictionary<int, byte[]> { [7] = [1], [8] = [2] };
        var (kept, missing) = IconTables.OfItems([Item(303, 7), Item(304, 7), Item(305, 9)], icons);

        Assert.Equal([7], kept.Keys);
        Assert.Equal(1, missing);
    }

    [Fact]
    public void AJobsIconIsKeptAndNoIconIsNone()
    {
        var icons = new Dictionary<int, byte[]> { [1] = [1], [2] = [2] };
        var kept = IconTables.OfJobs([Job(1, IconTables.NoIcon), Job(2, 1), Job(75, 1)], icons);

        Assert.Equal([1], kept.Keys);
    }

    [Fact]
    public void AJobWhoseIconTheClientLacksFails()
    {
        var failure = Assert.Throws<InvalidDataException>(() => IconTables.OfJobs([Job(2, 3)], new Dictionary<int, byte[]>()));

        Assert.Equal("Job 2 names icon 3, which the client lacks.", failure.Message);
    }

    [Fact]
    public void TheInstalledClientsItemsAndJobsHaveTheirIcons()
    {
        var client = Installed.Client("3.7.13.17");
        var texts = TextFiles.Open(client);
        var types = ItemTables.ReadTypes(DataTable.ReadById(client, "itemtypes"), texts);
        var (items, _) = ItemTables.Kept(ItemTables.ReadItems(DataTable.ReadById(client, "items"), texts), types);
        var textures = IconTables.ReadItemIcons(client);
        var (icons, missing) = IconTables.OfItems(items, textures);
        var jobs = IconTables.OfJobs(JobTables.ReadJobs(DataTable.ReadById(client, "jobs"), texts), IconTables.ReadJobIcons(client));

        // 3.7.13.17, artifacts/studies/items.md: four items name icons the bundle lacks, jobs 75 and 78 share one.
        Assert.Equal((12_295, 11_768, 4, 21), (textures.Count, icons.Count, missing, jobs.Count));

        // The three item icons docs/data.md names apart from the 64 px BC7 ones.
        using var bundle = Bundle.Open(Path.Combine(client.StreamingAssets, "Content", "Picto", "Items", "item_assets_1x.bundle"));
        var others = bundle.Objects(AssetClassID.Texture2D)
            .Select(asset =>
            {
                var reader = new ObjectReader(asset.Data, asset.Tree);
                var texture = Fields.Read(ref reader, 0, asset.Tree.Name);

                return (Name: texture.String("m_Name"), Format: texture.Int32("m_TextureFormat"), Side: texture.Int32("m_Width"));
            })
            .Where(texture => (texture.Format, texture.Side) != (Bc7, 64))
            .OrderBy(texture => texture.Name, StringComparer.Ordinal);

        Assert.Equal([("121179", Bc7, 60), ("25257", Rgba32, 62), ("82523", Bc7, 60)], others);
        Assert.All(icons.Values.Concat(jobs.Values), png =>
        {
            using var bitmap = SKBitmap.Decode(png);

            Assert.Equal((IconPictures.Size, IconPictures.Size), (bitmap.Width, bitmap.Height));
        });
    }

    private static Item Item(int id, int icon)
    {
        return new Item { Id = id, Name = _name, TypeId = 38, Level = 1, IconId = icon };
    }

    private static Job Job(int id, int icon)
    {
        return new Job { Id = id, Name = _name, IconId = icon };
    }

    // A square texture of RGBA32 texels, the bottom row first as Unity stores them; y counts from the bottom.
    private static byte[] Rgba(int side, Func<int, int, (int Red, int Green, int Blue, int Alpha)> texel)
    {
        var bytes = new byte[side * side * 4];

        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var (red, green, blue, alpha) = texel(x, y);
                var at = ((y * side) + x) * 4;

                (bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]) = ((byte)red, (byte)green, (byte)blue, (byte)alpha);
            }
        }

        return bytes;
    }
}
