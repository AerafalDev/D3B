using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.World;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestZoneTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private HarvestZone ZoneOf(HarvestSettings settings)
    {
        return HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
    }

    [Fact]
    public void AnAreaAloneStandsForItsSubAreasWhereTheResourcesGrow()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], Area = 1 });

        Assert.Equal([TestPlay.SubAreaId], zone.SubAreas);
        Assert.Equal(new[] { TestPlay.NorthMapId, TestPlay.MapId, TestPlay.EastMapId, TestPlay.InsideMapId, TestPlay.OtherInsideMapId }.Order(), zone.Maps.Keys.Order());
        Assert.Empty(ZoneOf(new HarvestSettings { Resources = [9_999], Area = 1 }).SubAreas);
        Assert.Empty(ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], Area = 2 }).Maps);
    }

    [Fact]
    public void SubAreasChosenStandForThemselvesOfAnyArea()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], Area = 2, SubAreas = [TestPlay.SubAreaId] });

        Assert.Equal([TestPlay.SubAreaId], zone.SubAreas);
        Assert.Equal(5, zone.Maps.Count);
        Assert.All(zone.Maps.Values, group => Assert.Equal(0, group));
    }

    [Fact]
    public void TheResourcesAskedForGiveTheirSkillsTheirPriority()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Chestnut, TestPlay.Ash], Area = 1 });

        Assert.Equal(new HarvestTarget(TestPlay.ChestnutSkillId, TestPlay.Chestnut, TestPlay.Lumberjack, 20, 0), zone.Targets[TestPlay.ChestnutSkillId]);
        Assert.Equal(new HarvestTarget(TestPlay.TreeSkillId, TestPlay.Ash, TestPlay.Lumberjack, 1, 1), zone.Targets[TestPlay.TreeSkillId]);
        Assert.Equal([TestPlay.Chestnut, TestPlay.Ash], zone.Wanted.Order().Reverse());
        Assert.Equal(2, zone.ListedIn(_play.World.Atlas).Count);
    }

    [Fact]
    public void ATourGoesThroughTheWorldMapsMapsBeforeTheInsides()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var sightings = new HarvestSightings();
        var tour = new HarvestTour(sightings, new GraphicSkills());

        Assert.Equal(new[] { TestPlay.InsideMapId, TestPlay.OtherInsideMapId }.Order(), zone.Insides.Order());
        Assert.Equal(new[] { TestPlay.NorthMapId, TestPlay.MapId, TestPlay.EastMapId }.Order(), tour.Remaining(zone).Order());

        tour.Visit(TestPlay.NorthMapId);
        tour.Visit(TestPlay.MapId);
        sightings.Seen(TestPlay.EastMapId, [], DateTimeOffset.UnixEpoch);

        // The maps of the world map gone through or seen empty, the insides come; the one without elements holds nothing.
        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));
        Assert.Equal(3, tour.Total(zone));
    }

    [Fact]
    public void AMapIsForetoldByWhatItsElementsGraphicsOfferElsewhere()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var graphics = new GraphicSkills();

        Assert.Equal(MapForecast.Unknown, zone.Foresee(TestPlay.NorthMapId, graphics));
        Assert.Equal(MapForecast.Without, zone.Foresee(TestPlay.OtherInsideMapId, graphics));

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);
        graphics.Learn(TestPlay.BushGfxId, [WorldPathfinder.DoorSkillId]);

        // The map's door is not known yet: its tree's graphic alone foretells it.
        Assert.Equal(MapForecast.Probable, zone.Foresee(TestPlay.MapId, graphics));
        Assert.Equal(MapForecast.Probable, zone.Foresee(TestPlay.NorthMapId, graphics));
        Assert.Equal(MapForecast.Without, zone.Foresee(TestPlay.EastMapId, graphics));
        Assert.Equal(MapForecast.Unknown, zone.Foresee(TestPlay.InsideMapId, graphics));
        Assert.Equal(MapForecast.Unknown, ZoneOf(new HarvestSettings { Resources = [TestPlay.Chestnut], SubAreas = [TestPlay.SubAreaId] }).Foresee(TestPlay.MapId, graphics));
    }

    [Fact]
    public void ATourGoesFirstToTheProbableMapsWhatABotSawWinning()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var sightings = new HarvestSightings();
        var graphics = new GraphicSkills();
        var tour = new HarvestTour(sightings, graphics);

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);
        graphics.Learn(TestPlay.BushGfxId, []);

        Assert.Equal(new[] { TestPlay.NorthMapId, TestPlay.MapId }.Order(), tour.Remaining(zone).Order());
        Assert.Equal(2, tour.Probable(zone));
        Assert.Equal(3, tour.Total(zone));

        sightings.Seen(TestPlay.NorthMapId, [], DateTimeOffset.UnixEpoch);

        Assert.Equal([TestPlay.MapId], tour.Remaining(zone));
        var states = tour.Maps(zone).ToDictionary(map => map.MapId, map => map.State);

        Assert.Equal(
            (ZoneMapState.Empty, ZoneMapState.Probable, ZoneMapState.Empty, ZoneMapState.ToVisit, ZoneMapState.Empty),
            (states[TestPlay.NorthMapId], states[TestPlay.MapId], states[TestPlay.EastMapId], states[TestPlay.InsideMapId], states[TestPlay.OtherInsideMapId]));

        tour.Visit(TestPlay.MapId);

        // The probable maps gone through, the others of the world map come, then the insides.
        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));
    }

    [Fact]
    public void EverythingKnownHarvestsEveryProfessionsResourceAlike()
    {
        var zone = ZoneOf(new HarvestSettings { EverythingKnown = true, Area = 1 });

        Assert.Equal([TestPlay.ChestnutSkillId, TestPlay.TreeSkillId], zone.Targets.Keys.Order());
        Assert.All(zone.Targets.Values, target => Assert.Equal(0, target.Priority));
        Assert.DoesNotContain(184, zone.Harvests.Keys);
    }
}
