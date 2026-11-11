using D3B.Extractor.Code;

namespace D3B.Extractor.Tests.Code;

public sealed class FlagEnumTests
{
    private static readonly FlagEnum _flags = FlagEnum.From("Test.MapFlags", [("None", 0), ("AllowTeleportTo", 4), ("AllowTeleportFrom", 8), ("Both", 12), ("HasPriority", 1UL << 23)]);

    [Fact]
    public void AValueIsTheNamesOfTheBitsItSetsInCamelCase()
    {
        Assert.Equal(["allowTeleportTo", "allowTeleportFrom", "hasPriority"], _flags.Names(4 | 8 | (1UL << 23)));
        Assert.Empty(_flags.Names(0));
    }

    [Fact]
    public void ABitTheEnumDoesNotNameFails()
    {
        var error = Assert.Throws<InvalidDataException>(() => _flags.Names(4 | 2));

        Assert.Contains("bit 1, which Test.MapFlags does not name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoMembersOfOneBitAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => FlagEnum.From("Test.MapFlags", [("A", 4), ("B", 4)]));
    }

    [Fact]
    public void TheInstalledClientNamesTheBitsOfAMapsFlags()
    {
        var client = Installed.Client();

        using var code = ClientCode.Open(client);

        var flags = code.FlagEnum("MapInformationFlags");

        Assert.NotEmpty(flags.Names(uint.MaxValue & 0b1100));
    }
}
