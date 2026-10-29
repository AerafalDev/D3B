using D3B.Data.Icons;

namespace D3B.Data.Tests.Icons;

public sealed class IconTableTests
{
    private static readonly int[] _descending = [2, 1];
    private static readonly int[] _twice = [1, 1];
    private static readonly int[] _two = [1, 2];
    private static readonly int[] _one = [1];

    [Fact]
    public void AnIconIsFoundByItsIdentifierWithItsOwnPixels()
    {
        var table = TestTables.Icons().Items;

        Assert.Equal(2, table.Count);
        Assert.True(table.TryGet(38_017, out var ash));
        Assert.Equal(IconTable.IconLength, ash.Length);
        Assert.All(ash.ToArray(), value => Assert.Equal(2, value));
        Assert.True(table.TryGet(34_009, out var wheat));
        Assert.All(wheat.ToArray(), value => Assert.Equal(1, value));
        Assert.False(table.TryGet(38_018, out _));
    }

    [Fact]
    public void IdentifiersOutOfOrderAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new IconTable(_descending, new byte[2 * IconTable.IconLength]));
        Assert.Throws<ArgumentException>(() => new IconTable(_twice, new byte[2 * IconTable.IconLength]));
    }

    [Fact]
    public void PixelsThatAreNotThoseOfEveryIconAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new IconTable(_two, new byte[IconTable.IconLength]));
        Assert.Throws<ArgumentException>(() => new IconTable(_one, new byte[IconTable.IconLength + 1]));
    }
}
