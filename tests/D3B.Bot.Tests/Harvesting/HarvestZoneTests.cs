using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Jobs;
using D3B.Data.World;
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
    public void ATourLeavesOutTheInteriorsOfASubAreaWithMapsOnTheWorldMapUnlessSomethingSaysTheyHoldAResource()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var sightings = new HarvestSightings();
        var tour = new HarvestTour(sightings, new GraphicSkills());

        Assert.Equal(new[] { TestPlay.InsideMapId, TestPlay.OtherInsideMapId }.Order(), zone.Interiors.Order());
        Assert.Equal(new[] { TestPlay.NorthMapId, TestPlay.MapId, TestPlay.EastMapId }.Order(), tour.Remaining(zone).Order());
        Assert.Equal(3, tour.Total(zone));

        tour.Visit(TestPlay.NorthMapId);
        tour.Visit(TestPlay.MapId);
        sightings.Seen(TestPlay.EastMapId, [], DateTimeOffset.UnixEpoch);

        // The maps of the world map gone through or seen empty, no building is entered: the houses of a town are measured to hold
        // nothing, and a player's report is that the bot went into all of them (D416).
        Assert.Empty(tour.Remaining(zone));
        Assert.Equal(ZoneMapState.Empty, tour.Maps(zone).Single(map => map.MapId == TestPlay.InsideMapId).State);
    }

    [Fact]
    public void AnInteriorSeenToHoldAResourceOrForetoldByAGraphicIsGoneThroughAfterTheMapsOfTheWorldMap()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var sightings = new HarvestSightings();
        var graphics = new GraphicSkills();
        var tour = new HarvestTour(sightings, graphics);

        tour.Visit(TestPlay.NorthMapId);
        tour.Visit(TestPlay.MapId);
        tour.Visit(TestPlay.EastMapId);

        // A mine is an interior of a sub-area that also holds maps on a world map: a bot that saw ore in it, or a graphic known to offer it, brings it back.
        sightings.Seen(TestPlay.InsideMapId, [TestPlay.Ash], DateTimeOffset.UnixEpoch);

        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));
        Assert.Equal(ZoneMapState.Probable, tour.Maps(zone).Single(map => map.MapId == TestPlay.InsideMapId).State);

        sightings = new HarvestSightings();
        tour = new HarvestTour(sightings, graphics);
        tour.Visit(TestPlay.NorthMapId);
        tour.Visit(TestPlay.MapId);
        tour.Visit(TestPlay.EastMapId);
        graphics.Learn(TestPlay.ChestGfxId, [TestPlay.TreeSkillId]);

        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));

        sightings.Seen(TestPlay.InsideMapId, [], DateTimeOffset.UnixEpoch);

        // What the bot saw there wins over the graphic: it held nothing.
        Assert.Empty(tour.Remaining(zone));
    }

    [Fact]
    public void AGraphicThatOffersOnlyWhatTheCharactersLevelCannotUseForetellsNoResourceButIsNotKnownToOfferNone()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Chestnut, TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var graphics = new GraphicSkills();

        // The tree's graphic offers the chestnut's skill alone, which asks level 20; the door's offers none a lumberjack uses.
        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.ChestnutSkillId]);
        graphics.Learn(TestPlay.DoorGfxId, [WorldPathfinder.DoorSkillId]);

        Assert.Equal(MapForecast.Probable, zone.Foresee(TestPlay.MapId, graphics));
        Assert.Equal(MapForecast.Unknown, zone.Foresee(TestPlay.MapId, graphics, _ => 19));
        Assert.Equal(MapForecast.Probable, zone.Foresee(TestPlay.MapId, graphics, _ => 20));

        // A graphic known to offer nothing the zone uses stays without, whatever the level.
        graphics.Learn(TestPlay.BushGfxId, [WorldPathfinder.DoorSkillId]);

        Assert.Equal(MapForecast.Without, zone.Foresee(TestPlay.EastMapId, graphics, _ => 1));
    }

    [Fact]
    public void ATourWeighsTheCharactersLevelAndALevelGainedBringsAMapBack()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Chestnut], SubAreas = [TestPlay.SubAreaId] });
        var graphics = new GraphicSkills();
        var tour = new HarvestTour(new HarvestSightings(), graphics);

        // Measured on the owner's graphics (D417): graphic 682 of Astrub's houses offers a lumberjack's skill of level 90.
        graphics.Learn(TestPlay.ChestGfxId, [TestPlay.ChestnutSkillId]);
        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.ChestnutSkillId]);
        graphics.Learn(TestPlay.DoorGfxId, [WorldPathfinder.DoorSkillId]);
        tour.Visit(TestPlay.NorthMapId);
        tour.Visit(TestPlay.MapId);
        tour.Visit(TestPlay.EastMapId);

        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));

        tour.Know(Lumberjack(19));

        // The house and the tree of the map are the same graphic: nothing a lumberjack of 19 can cut, so no map is probable.
        Assert.Empty(tour.Remaining(zone));
        Assert.Equal(0, tour.Probable(zone));
        Assert.Equal(ZoneMapState.Empty, tour.Maps(zone).Single(map => map.MapId == TestPlay.InsideMapId).State);

        tour.Know(Lumberjack(20));

        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));
        Assert.Equal(ZoneMapState.Probable, tour.Maps(zone).Single(map => map.MapId == TestPlay.InsideMapId).State);

        // A character whose professions the server has not described yet is not weighed.
        tour.Know(new CharacterState { Key = TestPlay.Key, Name = "Character" });

        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));
    }

    [Fact]
    public void AMapSeenHoldingOnlyWhatTheLevelCannotUseComesAfterTheProbableOnesAndAnInteriorIsLeftOutUntilTheLevelIsGained()
    {
        var zone = ZoneOf(new HarvestSettings { Resources = [TestPlay.Chestnut, TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var sightings = new HarvestSightings();
        var tour = new HarvestTour(sightings, new GraphicSkills());

        sightings.SeenElements(TestPlay.NorthMapId, [new SightedElement(1, TestPlay.ChestnutSkillId, TestPlay.Chestnut, true)], DateTimeOffset.UnixEpoch);
        sightings.SeenElements(TestPlay.MapId, [new SightedElement(2, TestPlay.TreeSkillId, TestPlay.Ash, true)], DateTimeOffset.UnixEpoch);
        sightings.Seen(TestPlay.EastMapId, [TestPlay.Ash], DateTimeOffset.UnixEpoch);
        sightings.SeenElements(TestPlay.InsideMapId, [new SightedElement(3, TestPlay.ChestnutSkillId, TestPlay.Chestnut, true)], DateTimeOffset.UnixEpoch);

        tour.Know(Lumberjack(1));

        // The ash grows on a map and the chestnut, which asks level 20, on another: the first is gone to first, and the second, being
        // outdoors, after it, as the server may offer it; the interior holding the chestnut alone is not entered.
        Assert.Equal(new[] { TestPlay.MapId, TestPlay.EastMapId }.Order(), tour.Remaining(zone).Order());

        tour.Visit(TestPlay.MapId);
        tour.Visit(TestPlay.EastMapId);

        Assert.Equal([TestPlay.NorthMapId], tour.Remaining(zone));

        tour.Visit(TestPlay.NorthMapId);

        Assert.Empty(tour.Remaining(zone));

        tour.Know(Lumberjack(20));

        Assert.Equal([TestPlay.InsideMapId], tour.Remaining(zone));
    }

    private static CharacterState Lumberjack(int level)
    {
        return new CharacterState { Key = TestPlay.Key, Name = "Character", Jobs = ImmutableSortedDictionary<int, CharacterJob>.Empty.Add(TestPlay.Lumberjack, new CharacterJob(TestPlay.Lumberjack, level, 0, 0, 0, [])) };
    }

    [Fact]
    public void ASubAreaOfInteriorsAloneIsADungeonAndKeepsAllItsMaps()
    {
        const int dungeon = 30;
        long[] ids = [1, 2, 3];
        var graph = new WorldGraph(
            ids.Select(id => new WorldVertex(id, 1)).ToArray(),
            new[] { new WorldEdge(0, 1, 0, 1), new WorldEdge(1, 2, 1, 1) },
            new[]
            {
                new WorldTransition(WorldTransitionType.Scroll, 0, 0, WorldTransition.None, 2, WorldTransition.None, WorldTransition.None),
                new WorldTransition(WorldTransitionType.Scroll, 0, 0, WorldTransition.None, 3, WorldTransition.None, WorldTransition.None),
            },
            Array.Empty<string>());
        var atlas = new WorldAtlas(
            ids.Select((id, index) => new MapPosition(id, (short)index, 0, dungeon, -1, 0, 0)).ToArray(),
            new[] { new SubArea(dungeon, 0, 1, 1, SubArea.NoZaap, true, 0, 1, 0, 0) },
            new[] { new Area(1, 0, 0) },
            Array.Empty<Waypoint>(),
            new[] { TestPlay.Ash },
            Array.Empty<int>());
        var zone = HarvestZone.Of(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [dungeon] }, atlas, graph, _play.Tables.Jobs, _play.Maps);
        var tour = new HarvestTour(new HarvestSightings(), new GraphicSkills());

        Assert.Empty(zone.Interiors);
        Assert.Equal([1L, 2L, 3L], tour.Remaining(zone).Order());
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
        Assert.Equal(2, tour.Total(zone));

        sightings.Seen(TestPlay.NorthMapId, [], DateTimeOffset.UnixEpoch);

        Assert.Equal([TestPlay.MapId], tour.Remaining(zone));
        var states = tour.Maps(zone).ToDictionary(map => map.MapId, map => map.State);

        Assert.Equal(
            (ZoneMapState.Empty, ZoneMapState.Probable, ZoneMapState.Empty, ZoneMapState.Empty, ZoneMapState.Empty),
            (states[TestPlay.NorthMapId], states[TestPlay.MapId], states[TestPlay.EastMapId], states[TestPlay.InsideMapId], states[TestPlay.OtherInsideMapId]));

        tour.Visit(TestPlay.MapId);

        // The probable maps gone through, no interior nothing says holds a resource is left (D416).
        Assert.Empty(tour.Remaining(zone));
    }

    [Fact]
    public void AMapReachedOnlyFarRoundIsIsolatedAndLeftOutUnlessTheSettingsTakeIt()
    {
        // Maps 1 to 3 hold together; map 5 is two changes from 3, through 4 of another sub-area, and map 9 five, through 4 and 6 to 8.
        const int forest = 10;
        const int other = 20;
        long[] ids = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        int[] subAreaOf = [forest, forest, forest, other, forest, other, other, other, forest];
        (int From, int To)[] links = [(0, 1), (1, 0), (1, 2), (2, 1), (2, 3), (3, 4), (3, 5), (5, 6), (6, 7), (7, 8)];
        var graph = new WorldGraph(
            ids.Select(id => new WorldVertex(id, 1)).ToArray(),
            links.Select((link, index) => new WorldEdge(link.From, link.To, index, 1)).ToArray(),
            links.Select(link => new WorldTransition(WorldTransitionType.Scroll, 0, 0, WorldTransition.None, ids[link.To], WorldTransition.None, WorldTransition.None)).ToArray(),
            Array.Empty<string>());
        var atlas = new WorldAtlas(
            ids.Select((id, index) => new MapPosition(id, (short)index, 0, subAreaOf[index], 1, 0, 0)).ToArray(),
            new[] { new SubArea(forest, 0, 1, 1, SubArea.NoZaap, true, 0, 1, 0, 0), new SubArea(other, 0, 1, 1, SubArea.NoZaap, true, 1, 0, 0, 0) },
            new[] { new Area(1, 0, 0) },
            Array.Empty<Waypoint>(),
            new[] { TestPlay.Ash },
            Array.Empty<int>());
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [forest] };
        var zone = HarvestZone.Of(settings, atlas, graph, _play.Tables.Jobs, _play.Maps);
        var tour = new HarvestTour(new HarvestSightings(), new GraphicSkills());

        Assert.Equal([9L], zone.Isolated);
        Assert.Equal([1L, 2L, 3L, 5L], tour.Remaining(zone).Order());
        Assert.Equal(ZoneMapState.Isolated, tour.Maps(zone).Single(map => map.MapId == 9).State);
        Assert.Equal(4, tour.Total(zone));

        settings.IsolatedMaps = true;
        zone = HarvestZone.Of(settings, atlas, graph, _play.Tables.Jobs, _play.Maps);
        tour = new HarvestTour(new HarvestSightings(), new GraphicSkills());

        // Taken, it comes last, once the rest of its sub-area is gone through.
        Assert.Equal([1L, 2L, 3L, 5L], tour.Remaining(zone).Order());

        foreach (var map in new long[] { 1, 2, 3, 5 })
            tour.Visit(map);

        Assert.Equal([9L], tour.Remaining(zone));
    }

    [Fact]
    public void BasesResourcesAreHarvestedWhenAskedForOrWithoutAProfessionChosen()
    {
        const int well = 102;
        const int water = 311;
        var jobs = new JobCatalog(
            _play.Tables.Jobs.Jobs,
            _play.Tables.Jobs.Skills.ToArray().Append(new Skill(well, 0, Job.Base, water, 1, 1, false)).OrderBy(skill => skill.Id).ToArray());
        var asked = HarvestZone.Of(new HarvestSettings { Resources = [water, TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, _play.World.Atlas, _play.World.Graph, jobs, _play.Maps);
        var professions = HarvestZone.Of(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], SubAreas = [TestPlay.SubAreaId] }, _play.World.Atlas, _play.World.Graph, jobs, _play.Maps);
        var without = HarvestZone.TargetsOf(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack, Job.Base] }, jobs);
        var graphics = new GraphicSkills();

        Assert.Equal(new HarvestTarget(well, water, Job.Base, 1, 0), asked.Targets[well]);
        Assert.DoesNotContain(well, professions.Targets.Keys);
        Assert.Equal(water, professions.Harvests[well]);
        Assert.Equal(new HarvestTarget(well, water, Job.Base, 1, 0), without[well]);

        // No sub-area lists it: only a graphic known to offer its skill tells the zone holds it.
        Assert.DoesNotContain(water, asked.PresentIn(_play.World.Atlas, graphics));
        graphics.Learn(TestPlay.TreeGfxId, [well]);
        Assert.Contains(water, asked.PresentIn(_play.World.Atlas, graphics));
        Assert.Equal(MapForecast.Probable, asked.Foresee(TestPlay.NorthMapId, graphics));
    }

    [Fact]
    public void AnAreaAloneStandsForTheSubAreasWhereTheLearnedGraphicsPlaceBasesResources()
    {
        const int well = 102;
        const int water = 311;
        var jobs = new JobCatalog(
            _play.Tables.Jobs.Jobs,
            _play.Tables.Jobs.Skills.ToArray().Append(new Skill(well, 0, Job.Base, water, 1, 1, false)).OrderBy(skill => skill.Id).ToArray());
        var settings = new HarvestSettings { Resources = [water], Area = 1 };
        var graphics = new GraphicSkills();

        Assert.Empty(HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, jobs, _play.Maps, HarvestZone.PlacedFor(settings, jobs, _play.Places, graphics)).SubAreas);

        graphics.Learn(TestPlay.TreeGfxId, [well]);

        Assert.Equal([TestPlay.SubAreaId], HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, jobs, _play.Maps, HarvestZone.PlacedFor(settings, jobs, _play.Places, graphics)).SubAreas);
        Assert.Null(HarvestZone.PlacedFor(new HarvestSettings { Resources = [water], SubAreas = [TestPlay.SubAreaId] }, jobs, _play.Places, graphics));
    }

    [Fact]
    public void ByProfessionHarvestsTheProfessionsResourcesAlikeOrTheHighestLevelFirst()
    {
        var zone = ZoneOf(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Area = 1 });
        var highest = HarvestZone.TargetsOf(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Order = HarvestOrder.HighestLevel }, _play.Tables.Jobs);

        Assert.Equal([TestPlay.ChestnutSkillId, TestPlay.TreeSkillId], zone.Targets.Keys.Order());
        Assert.All(zone.Targets.Values, target => Assert.Equal(0, target.Priority));
        Assert.DoesNotContain(184, zone.Harvests.Keys);
        Assert.True(highest[TestPlay.ChestnutSkillId].Priority < highest[TestPlay.TreeSkillId].Priority);

        // Taking the professions in turn starts from the highest level first, which the activity then orders by profession (D391).
        var balanced = HarvestZone.TargetsOf(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Order = HarvestOrder.Balanced }, _play.Tables.Jobs);

        Assert.Equal(highest, balanced);
    }

    [Fact]
    public void AResourceLeftOutIsNeverATargetAndAListKeepsItsOrder()
    {
        var excluded = HarvestZone.TargetsOf(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Excluded = [TestPlay.Chestnut] }, _play.Tables.Jobs);
        var list = HarvestZone.TargetsOf(new HarvestSettings { Resources = [TestPlay.Chestnut, TestPlay.Ash], Jobs = [TestPlay.Lumberjack] }, _play.Tables.Jobs);

        Assert.Equal([TestPlay.TreeSkillId], excluded.Keys);
        Assert.Equal(0, list[TestPlay.ChestnutSkillId].Priority);
        Assert.Equal(1, list[TestPlay.TreeSkillId].Priority);
    }

    [Fact]
    public void ThePlaceTheActivityChoseStandsForTheSettingsAreaAndSubAreas()
    {
        var settings = new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Place = HarvestPlace.Automatic };

        Assert.Empty(ZoneOf(settings).SubAreas);
        Assert.Equal([TestPlay.SubAreaId], HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps, chosen: [TestPlay.SubAreaId]).SubAreas);
        Assert.Null(HarvestZone.PlacedFor(new HarvestSettings { Place = HarvestPlace.Automatic, Area = 1 }, _play.Tables.Jobs, _play.Places, new GraphicSkills()));
    }
}
