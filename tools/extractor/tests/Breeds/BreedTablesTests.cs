using D3B.Extractor.Breeds;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using SkiaSharp;

namespace D3B.Extractor.Tests.Breeds;

public sealed class BreedTablesTests
{
    private static readonly Breed _iop = new() { Id = 8, Name = new Text { Fr = "Iop", En = "Iop", De = "Iop", Es = "Yopuka", Pt = "Iop" } };
    private static readonly Breed _cra = new() { Id = 9, Name = new Text { Fr = "Crâ", En = "Cra", De = "Crâ", Es = "Ocra", Pt = "Cra" } };

    [Fact]
    public void BreedsAreWrittenAsTheAppReadsThem()
    {
        // The app's own tests read the same fixture (src-tauri/src/data/breeds.rs).
        var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "breeds", "breeds.json"));

        Assert.Equal(expected, BreedTables.Write([_iop, _cra]));
    }

    [Fact]
    public void AHeadIsNamedByItsBreedAndSex()
    {
        Assert.Equal("heads/8-0.png", BreedTables.PathOf(8, 0));
        Assert.Equal("heads/20-1.png", BreedTables.PathOf(20, 1));
    }

    [Fact]
    public void ABreedsNameIsTheNumberItsStringHolds()
    {
        Assert.Equal(693, BreedTables.NameId(8, "693"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Iop")]
    [InlineData("-693")]
    [InlineData(" 693")]
    public void ABreedsNameThatIsNoNumberFails(string value)
    {
        var failure = Assert.Throws<InvalidDataException>(() => BreedTables.NameId(8, value));

        Assert.Contains("Breed 8", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryBreedWithBothHeadsPasses()
    {
        BreedTables.Check([_iop, _cra], [(8, 0), (8, 1), (9, 0), (9, 1)]);
    }

    [Fact]
    public void ABreedWithoutItsFemaleHeadFails()
    {
        var failure = Assert.Throws<InvalidDataException>(() => BreedTables.Check([_iop, _cra], [(8, 0), (8, 1), (9, 0)]));

        Assert.Equal("Breed 9 has no head for sex 1.", failure.Message);
    }

    [Fact]
    public void AHeadOfABreedTheTableLacksFails()
    {
        var failure = Assert.Throws<InvalidDataException>(() => BreedTables.Check([_iop], [(8, 0), (8, 1), (19, 0)]));

        Assert.Equal("The client draws a head of sex 0 for breed 19, which its table does not hold.", failure.Message);
    }

    [Fact]
    public void AHeadIsDrawnUprightWithItsTransparency()
    {
        // One 4x4 BC7 block in mode 6 is one colour here: an opaque red block above a transparent one, stored bottom row first.
        var transparent = Bc7Block(0, 0, 0, 0);
        var red = Bc7Block(255, 0, 0, 255);
        using var bitmap = SKBitmap.Decode(HeadPictures.Png([.. transparent, .. red], 4, 8));

        Assert.Equal((4, 8), (bitmap.Width, bitmap.Height));
        var top = bitmap.GetPixel(0, 0);

        Assert.True(top.Red > 250 && top.Green is 0 && top.Alpha > 250, $"The top row is {top}, not opaque red.");
        Assert.Equal(0, bitmap.GetPixel(0, 7).Alpha);
    }

    [Fact]
    public void TheInstalledClientsBreedsHaveTheirHeads()
    {
        var client = Installed.Client();
        var breeds = BreedTables.ReadBreeds(DataTable.ReadById(client, "breeds"), TextFiles.Open(client));
        var heads = HeadPictures.Read(client);

        BreedTables.Check(breeds, heads.Keys);
        Assert.NotEmpty(breeds);
        Assert.All(heads.Values, png =>
        {
            using var bitmap = SKBitmap.Decode(png);

            Assert.Equal((HeadPictures.Size, HeadPictures.Size), (bitmap.Width, bitmap.Height));
        });
    }

    // A mode 6 block whose two endpoints are the colour, to within its last bit, and whose indices are all 0: every texel is that colour.
    private static byte[] Bc7Block(int red, int green, int blue, int alpha)
    {
        // Mode 6: bit 6 set; then R0 R1 G0 G1 B0 B1 A0 A1 at 7 bits each, two p-bits left at 0, then 63 bits of indices at 0.
        var bits = new System.Numerics.BigInteger(1 << 6);
        var offset = 7;

        foreach (var value in new[] { red, red, green, green, blue, blue, alpha, alpha })
        {
            bits |= new System.Numerics.BigInteger(value >> 1) << offset;
            offset += 7;
        }

        var bytes = bits.ToByteArray(isUnsigned: true, isBigEndian: false);

        return [.. bytes, .. new byte[16 - bytes.Length]];
    }
}
