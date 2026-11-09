using D3B.Data.Hints;
using D3B.Data.Icons;

namespace D3B.Data.Tests.Hints;

public sealed class HintCatalogTests
{
    private static readonly int[] _icons = [411, 422];

    private static HintCatalog Catalog()
    {
        return new HintCatalog(
            new Hint[]
            {
                new(2737, 4, 411, 3431, 219_677_441, 1, -25, 36, 0),
                new(2747, 6, 422, 3432, 212_732_163, 1, -36, -60, 110),
                new(2750, 6, 422, 3433, 216_927_751, 9, -28, 37, 110),
            },
            new HintCategory[] { new(4, 3060), new(6, 3062) },
            new IconTable(1, _icons, new byte[8]));
    }

    [Fact]
    public void AHintAndACategoryAreFoundByTheirId()
    {
        var catalog = Catalog();

        Assert.True(catalog.TryGetHint(2747, out var hint));
        Assert.Equal(422, hint.Gfx);
        Assert.Equal(110, hint.Level);
        Assert.Equal(-36, hint.X);
        Assert.Equal(-60, hint.Y);
        Assert.Equal(212_732_163, hint.MapId);
        Assert.True(catalog.TryGetCategory(6, out var category));
        Assert.Equal(3062, category.NameId);
    }

    [Fact]
    public void WhatTheCatalogLacksIsNotFound()
    {
        var catalog = Catalog();

        Assert.False(catalog.TryGetHint(1, out _));
        Assert.False(catalog.TryGetCategory(9, out _));
    }

    [Fact]
    public void TheHintsOfAWorldAreThoseThatStandOnIt()
    {
        var catalog = Catalog();

        Assert.Equal([2737, 2747], catalog.HintsOn(1).Select(hint => hint.Id).ToArray());
        Assert.Equal([2750], catalog.HintsOn(9).Select(hint => hint.Id).ToArray());
        Assert.Empty(catalog.HintsOn(2));
    }

    [Fact]
    public void TheIconsAreKeyedByTheHintsGraphic()
    {
        var catalog = Catalog();

        Assert.True(catalog.Icons.TryGet(catalog.HintsOn(1)[1].Gfx, out var pixels));
        Assert.Equal(4, pixels.Length);
        Assert.False(catalog.Icons.TryGet(410, out _));
    }

    [Fact]
    public void ATableOutOfOrderOrWithARepeatedIdIsRejected()
    {
        var icons = new IconTable(1, Array.Empty<int>(), Array.Empty<byte>());

        Assert.Throws<ArgumentException>(() => new HintCatalog(new Hint[] { new(2, 4, 1, 1, 1, 1, 0, 0, 0), new(1, 4, 1, 1, 1, 1, 0, 0, 0) }, Array.Empty<HintCategory>(), icons));
        Assert.Throws<ArgumentException>(() => new HintCatalog(Array.Empty<Hint>(), new HintCategory[] { new(4, 1), new(4, 2) }, icons));
    }
}
