using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Localization;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestPreviewsTests : IAsyncDisposable
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static string Say(GameText text)
    {
        var format = text.Many is { } many && text.Count != 1 ? many : text.Format;

        return string.Format(_english, format.In(_english), [.. text.Values.Select(value => value.Text ?? value.Id.ToString(_english))]);
    }

    [Fact]
    public void ThePreviewCountsTheZonesMapsItsResourcesAndTheWayToTheNearestBank()
    {
        var previews = new HarvestPreviews(_play.World, _play.Tables);
        var preview = previews.Of(new HarvestSettings { Resources = [TestPlay.Chestnut, TestPlay.Ash, 9_999], Area = 1 })!;

        Assert.Equal(
            ["5: maps to go through", "2 of 3: resources present", "1 map: to the nearest bank"],
            preview.Figures.Select(figure => $"{Say(figure.Value)}: {Say(figure.Label)}"));
        Assert.Equal(5, preview.Zone.Length);
        Assert.All(preview.Zone, map => Assert.Equal(ZoneMapState.ToVisit, map.State));
        Assert.Equal(TestPlay.MapId, preview.LandmarkMapId);
    }

    [Fact]
    public void SettingsNamingNoResourceOrNoPlaceHaveNoPreview()
    {
        var previews = new HarvestPreviews(_play.World, _play.Tables);

        Assert.Null(previews.Of(new HarvestSettings { Area = 1 }));
        Assert.Null(previews.Of(new HarvestSettings { Resources = [TestPlay.Ash] }));
    }

    [Fact]
    public void ThePreviewOfTheSameZoneAndResourcesIsKept()
    {
        var previews = new HarvestPreviews(_play.World, _play.Tables);
        var first = previews.Of(new HarvestSettings { Resources = [TestPlay.Ash], Area = 1 });

        Assert.Same(first, previews.Of(new HarvestSettings { Resources = [TestPlay.Ash], Area = 1, FullAt = 70 }));
        Assert.NotSame(first, previews.Of(new HarvestSettings { Resources = [TestPlay.Chestnut], Area = 1 }));
    }
}
