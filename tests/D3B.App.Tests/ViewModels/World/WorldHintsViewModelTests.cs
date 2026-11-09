using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels.World;
using D3B.Data.Hints;

namespace D3B.App.Tests.ViewModels.World;

[UiThread]
public sealed class WorldHintsViewModelTests : IDisposable
{
    private readonly TestGameData _data = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public void Dispose()
    {
        _data.Dispose();
    }

    private WorldHintsViewModel Hints(int world = 1)
    {
        var hints = new WorldHintsViewModel(_localizer);

        hints.Use(_data.Tables, _data.Texts);
        hints.UseLayer(world);

        return hints;
    }

    [Fact]
    public void WithoutHintsInThePackNothingIsOfferedOrDrawn()
    {
        var hints = new WorldHintsViewModel(_localizer);

        hints.Use(null, null);
        hints.UseLayer(1);

        Assert.False(hints.IsAvailable);
        Assert.Empty(hints.Categories);
        Assert.Empty(hints.Visible);
    }

    [Fact]
    public void TheCategoriesAreNamedByTheGamesOwnTextsInTheLanguageOfTheApp()
    {
        var hints = Hints();

        Assert.True(hints.IsAvailable);
        Assert.Equal(["Workshops", "Dungeons", "Transports"], hints.Categories.Select(category => category.Name));

        _localizer.Use(Localizer.French);
        hints.Relabel();

        Assert.Equal(["Ateliers", "Donjons", "Transports"], hints.Categories.Select(category => category.Name));
        Assert.Equal("Zaap", hints.Visible.Single(hint => hint.Id == 1).Name);
        Assert.Equal("Garde-manger du Rat Blanc", hints.Visible.Single(hint => hint.Id == 2).Name);
    }

    [Fact]
    public void EachCategoryCountsItsHintsOnTheWorldShown()
    {
        var hints = Hints();

        Assert.Equal([1, 1, 1], hints.Categories.Select(category => category.Count));

        hints.UseLayer(2);

        Assert.Equal([0, 0, 1], hints.Categories.Select(category => category.Count));
    }

    [Fact]
    public void TheMapDrawsTheHintsOfTheWorldShownThatTheTickedCategoriesHold()
    {
        var hints = Hints();

        Assert.Equal([1, 2, 3], hints.Visible.Select(hint => hint.Id).Order());

        hints.Categories.Single(category => category.Id == 9).IsOn = false;

        Assert.Equal([2, 3], hints.Visible.Select(hint => hint.Id).Order());

        hints.UseLayer(2);

        Assert.Empty(hints.Visible);

        hints.Categories.Single(category => category.Id == 9).IsOn = true;

        Assert.Equal([4], hints.Visible.Select(hint => hint.Id));
    }

    [Fact]
    public void OneSwitchShowsOrHidesAllTheHintsAndKeepsTheCategoriesTicked()
    {
        var hints = Hints();

        hints.Categories.Single(category => category.Id == 6).IsOn = false;
        hints.ShowHints = false;

        Assert.Empty(hints.Visible);

        hints.ShowHints = true;

        Assert.Equal([1, 3], hints.Visible.Select(hint => hint.Id).Order());
    }

    [Fact]
    public void AHintCarriesItsPictureAndItsLevelAndLacksThePictureThePackDoesNotHold()
    {
        var hints = Hints(2);

        Assert.Null(hints.Visible.Single().Icon);

        hints.UseLayer(1);

        Assert.All(hints.Visible, hint => Assert.NotNull(hint.Icon));
        Assert.Equal(110, hints.Visible.Single(hint => hint.Id == 2).Level);
    }

    [Fact]
    public void TheHoverNamesTheHintItsCategoryItsLevelAndWhereItStands()
    {
        var hints = Hints();

        Assert.Equal("Zaap · Transports · 3,0", hints.Describe(new WorldMapHit(WorldMapHitKind.Hint, 3, 0, -1, -1, "1")));
        Assert.Equal("White Rat Larder · Dungeons · level 110 · 2,0", hints.Describe(new WorldMapHit(WorldMapHitKind.Hint, 2, 0, -1, -1, "2")));
        Assert.Null(hints.Describe(new WorldMapHit(WorldMapHitKind.Hint, 2, 0, -1, -1, "99")));
        Assert.Null(hints.Describe(new WorldMapHit(WorldMapHitKind.Square, 2, 0, -1, -1)));
    }

    [Theory]
    [InlineData(9, 410, 100)]
    [InlineData(4, 401, 90)]
    [InlineData(6, 422, 80)]
    [InlineData(3, 311, 70)]
    [InlineData(2, 227, 60)]
    [InlineData(1, 80, 50)]
    [InlineData(9, 413, 40)]
    [InlineData(4, 408, 30)]
    [InlineData(4, 902, 10)]
    public void TheMostUsefulHintsAreZaapsBanksDungeonsAndWorkshopsAndPassagesAreTheLeast(int category, int gfx, int priority)
    {
        // The categories of the table mix: 4 holds the banks, the taverns and the passages, 9 the zaaps and the boats, so the graphic tells them apart.
        var hint = new Hint(1, category, gfx, 1, 1, 1, 0, 0, 0);

        Assert.Equal(priority, HintPriority.Of(hint));
    }
}
