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
        // Columns of opaque red and of transparent black: averaged unpremultiplied, the icon would turn dark red.
        var texels = Rgba(64, (x, _) => x % 2 == 0 ? (255, 0, 0, 255) : (0, 0, 0, 0));
        using var bitmap = SKBitmap.Decode(IconPictures.Png(texels, Rgba32, 64));
        var pixel = bitmap.GetPixel(10, 10);

        Assert.True(pixel.Red > 250 && pixel.Alpha is > 120 and < 136, $"A half transparent pixel is {pixel}, not red at half alpha.");
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
    public void ATextureOfAnotherFormatSizeOrLengthFails(int format, int side, int length)
    {
        var failure = Assert.Throws<InvalidDataException>(() => IconPictures.Png(new byte[length], format, side));

        Assert.Equal($"An icon of {side} px in format {format} holds {length} bytes: not BC7 or RGBA32 of at least 32 px.", failure.Message);
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
        var textures = IconPictures.Read(client, "Content", "Picto", "Items", "item_assets_1x.bundle");
        var (icons, missing) = IconTables.OfItems(items, textures);
        var jobs = IconTables.OfJobs(JobTables.ReadJobs(DataTable.ReadById(client, "jobs"), texts), IconPictures.Read(client, "Content", "Picto", "UI", "job_assets_1x.bundle"));

        // 3.7.13.17, `artifacts/studies/items.md`: four items name icons the bundle lacks, jobs 75 and 78 share one.
        Assert.Equal((12_295, 11_768, 4, 21), (textures.Count, icons.Count, missing, jobs.Count));
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
