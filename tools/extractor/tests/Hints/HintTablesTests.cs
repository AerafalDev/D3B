using D3B.Extractor.Hints;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using D3B.Extractor.World;
using SkiaSharp;

namespace D3B.Extractor.Tests.Hints;

public sealed class HintTablesTests
{
    private const int Rgba32 = 4;
    private const int Dxt5 = 12;

    private static readonly Text _bank = new() { Fr = "Banque", En = "Bank", De = "Bank", Es = "Banco", Pt = "Banco" };
    private static readonly Text _various = new() { Fr = "Divers", En = "Miscellaneous", De = "Verschiedenes", Es = "Varios", Pt = "Diversos" };

    private static readonly Hint _astrubBank = new()
    {
        Id = 2733,
        CategoryId = 4,
        Gfx = 401,
        Name = _bank,
        MapId = 191104002,
        X = 4,
        Y = -18,
        Outdoor = true,
        SubAreaId = 95,
        WorldMapId = 1,
        Level = 0,
    };

    [Fact]
    public void HintsAndTheirCategoriesAreWrittenAsTheAppReadsThem()
    {
        Hint[] hints = [_astrubBank, new() { Id = 2800, CategoryId = 6, Gfx = 422, Name = _bank, MapId = 7, X = -2, Y = 3, Outdoor = false, SubAreaId = 96, WorldMapId = 2, Level = 50 }];

        // The app's own tests read the same fixtures (src-tauri/src/data/hints.rs).
        Assert.Equal(Fixture("hints.json"), HintTables.WriteHints(hints));
        Assert.Equal(Fixture("hint-categories.json"), HintTables.WriteCategories([new HintCategory { Id = 4, Name = _various }]));
    }

    [Fact]
    public void APictureIsNamedByItsNumber()
    {
        Assert.Equal("icons/hints/401.png", HintTables.PathOf(401));
    }

    [Fact]
    public void AHintOfNoCategoryOrOnNoMapIsRefused()
    {
        HintCategory[] categories = [new() { Id = 4, Name = _various }];
        MapPosition[] positions = [new() { Id = 191104002, X = 4, Y = -18, SubAreaId = 95, WorldMap = 1, Name = null, Flags = [] }];

        HintTables.Check([_astrubBank], categories, positions);

        var orphan = Assert.Throws<InvalidDataException>(() => HintTables.Check([_astrubBank], [new HintCategory { Id = 6, Name = _various }], positions));
        var lost = Assert.Throws<InvalidDataException>(() => HintTables.Check([_astrubBank], categories, []));

        Assert.Contains("category 4", orphan.Message, StringComparison.Ordinal);
        Assert.Contains("map 191104002", lost.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Dxt5, 64, 64, 1, 1)]
    [InlineData(Rgba32, 62, 62, 1, 1)]
    public void APictureOfTheMeasuredShapeIsRead(int format, int width, int height, int levels, int images)
    {
        HintPictures.CheckShape("401", format, width, height, levels, images);
    }

    [Theory]
    [InlineData(Dxt5, 32, 32, 1, 1)]
    [InlineData(Rgba32, 64, 64, 1, 1)]
    [InlineData(Dxt5, 64, 32, 1, 1)]
    [InlineData(Dxt5, 64, 64, 7, 1)]
    [InlineData(25, 64, 64, 1, 1)]
    public void APictureOfAnotherShapeIsRefused(int format, int width, int height, int levels, int images)
    {
        Assert.Throws<InvalidDataException>(() => HintPictures.CheckShape("401", format, width, height, levels, images));
    }

    [Fact]
    public void ADxt5PictureIsDecodedUprightWithItsTransparencyAtItsSize()
    {
        // Sixteen rows of sixteen blocks, the bottom row first: transparent below, opaque red above.
        byte[] transparent = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] red = [255, 255, 0, 0, 0, 0, 0, 0, 0x00, 0xF8, 0x00, 0xF8, 0, 0, 0, 0];
        byte[] blocks = [.. Enumerable.Range(0, 256).SelectMany(block => block < 128 ? transparent : red)];
        using var bitmap = SKBitmap.Decode(HintPictures.Png(blocks, Dxt5, 64));

        Assert.Equal((64, 64), (bitmap.Width, bitmap.Height));
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(0, bitmap.GetPixel(63, 63).Alpha);
    }

    [Fact]
    public void AnRgba32PictureIsKeptAtItsSizeUpright()
    {
        // The bottom row first: its first row blue, the others green.
        byte[] texels = [.. Enumerable.Range(0, 62 * 62).SelectMany(texel => texel < 62 ? new byte[] { 0, 0, 255, 255 } : [0, 255, 0, 255])];
        using var bitmap = SKBitmap.Decode(HintPictures.Png(texels, Rgba32, 62));

        Assert.Equal((62, 62), (bitmap.Width, bitmap.Height));
        Assert.Equal(new SKColor(0, 0, 255, 255), bitmap.GetPixel(0, 61));
        Assert.Equal(new SKColor(0, 255, 0, 255), bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void APictureWhoseBytesAreNotItsSidesIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => HintPictures.Png(new byte[100], Dxt5, 64));
        Assert.Throws<InvalidDataException>(() => HintPictures.Png(new byte[62 * 62 * 4], 25, 62));
    }

    [Fact]
    public void TheInstalledClientsHintsAreAsMeasured()
    {
        var client = Installed.Client("3.7.13.17");
        var texts = TextFiles.Open(client);
        var categories = HintTables.ReadCategories(DataTable.ReadById(client, "hintcategories"), texts);
        var hints = HintTables.ReadHints(DataTable.ReadById(client, "hints"), texts);
        var pictures = HintPictures.Read(client, hints.Select(hint => hint.Gfx).ToHashSet());

        // 3.7.13.17, main:docs/client.md, "Hints": 835 hints in 6 of 12 categories, 91 of their 92 pictures, 475's missing.
        Assert.Equal((835, 12, 6), (hints.Count, categories.Count, hints.Select(hint => hint.CategoryId).Distinct().Count()));
        Assert.Equal((92, 91), (hints.Select(hint => hint.Gfx).Distinct().Count(), pictures.Count));
        Assert.DoesNotContain(475, pictures.Keys);
        Assert.Equal(10, hints.Count(hint => hint.Gfx == 401));

        using var zaap = SKBitmap.Decode(pictures[410]);
        using var classPlace = SKBitmap.Decode(pictures[10]);

        Assert.Equal((64, 62), (zaap.Width, classPlace.Width));
    }

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "hints", name));
    }
}
