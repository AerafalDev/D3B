using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Fighting;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Fighting;

/// <summary>What fighting settings lead to, for the profile editor (D245): the ground's maps, its monsters and their levels, its bank, and the place for each character.</summary>
public sealed class FightPreviewsTests : IAsyncDisposable
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
    public void ThePreviewCountsTheGroundsMapsItsMonstersAndTheWayToTheNearestBank()
    {
        var previews = new FightPreviews(_play.World, _play.Tables);
        var settings = new FightSettings { Mode = FightMode.Chosen, Monsters = [489], Place = FightPlace.Chosen, SubAreas = [TestPlay.SubAreaId] };
        var preview = previews.Of(settings)!;
        var ground = HuntingGround.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Monsters, 1, subscribed: true);

        // The test's world lists no monster in its sub-area.
        Assert.Equal(
            [$"{ground.Maps.Count}: maps to go through", "0 of 1: monsters chosen living there", "—: levels of the monsters", "1 map: to the nearest bank"],
            preview.Figures.Select(figure => $"{Say(figure.Value)}: {Say(figure.Label)}"));
        Assert.Equal(ground.Maps.Keys.Order(), preview.Zone.Select(map => map.MapId).Order());
        Assert.All(preview.Zone, map => Assert.Equal(ZoneMapState.ToVisit, map.State));
        Assert.Equal(TestPlay.MapId, preview.LandmarkMapId);
        Assert.Same(preview, previews.Of(settings));
        Assert.Null(previews.Of(new FightSettings { Place = FightPlace.Chosen, Area = 404 }));
    }

    [Fact]
    public void ThePlaceTheActivityChoosesIsSaidForEachCharacterByItsLevel()
    {
        var previews = new FightPreviews(_play.World, _play.Tables);
        PreviewPlayer[] players = [new("Ortie", 20, new Dictionary<int, int>(), null), new("Unseen", 0, new Dictionary<int, int>(), null)];
        var preview = previews.Of(new FightSettings(), players)!;

        // No sub-area of the test's world lists a monster: the place is chosen for none.
        Assert.Equal(["Ortie: No monster to fight at its level", "Unseen: Level not seen yet"], preview.PlayerNotes.Select(note => $"{note.Name}: {Say(note.Note)}"));
        Assert.Equal(["Auto: place chosen for each character"], preview.Figures.Select(figure => $"{Say(figure.Value)}: {Say(figure.Label)}"));
        Assert.Empty(preview.Zone);
    }
}
