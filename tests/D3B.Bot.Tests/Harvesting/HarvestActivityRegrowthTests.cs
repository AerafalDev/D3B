using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>The harvest's journey staying on the map it is about to leave, where a wanted resource grew back since it came (D495).</summary>
public sealed class HarvestActivityRegrowthTests
{
    private const int NorthTreeInstanceId = 4_478;

    // The north map's own tree, on the map the character stands on: offered or harvested.
    private static InteractiveElement NorthTree(bool enabled, int skillId = TestPlay.TreeSkillId)
    {
        var skill = new InteractiveSkill { SkillId = skillId, InstanceId = NorthTreeInstanceId };
        var tree = new InteractiveElement { ElementId = TestPlay.NeighboursElementId, InteractiveId = 34, OnCurrentMap = true };

        (enabled ? tree.EnabledSkills : tree.DisabledSkills).Add(skill);

        return tree;
    }

    // The character on the north map, its tree harvested, the tour going on to the middle map below by a scroll.
    private static async Task<(TestConnection Connection, ActivityRun Run)> LeavingTheNorthMapAsync(TestPlay play, int skillId = TestPlay.TreeSkillId)
    {
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var information = TestPlay.Information(300, TestPlay.NorthMapId);

        information.InteractiveElements.Add(NorthTree(enabled: false, skillId));
        connection.Server(information);
        await bot.ProcessedAsync();

        var activity = new HarvestActivity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, play.World, play.Tables, play.Maps, new HarvestSightings(), new GraphicSkills(), play.Places, new Regrowths(), play.Time);
        var run = bot.StartActivity(activity);
        var walk = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1));

        Assert.True((walk.KeyMovements[^1] & 0xFFF) >= 532);

        return (connection, run);
    }

    [Fact]
    public async Task AResourceThatGrowsBackAsTheCharacterWalksOffItsMapIsHarvestedBeforeItLeaves()
    {
        // Measured on the owner's captures of 18 to 22 November: 136 resources grew back as the walk to the map's edge went, and the
        // character changed map without them.
        await using var play = new TestPlay();
        var (connection, run) = await LeavingTheNorthMapAsync(play);

        connection.Server(new InteractiveElementUpdatedEvent { Element = NorthTree(enabled: true) });
        await TestWalks.WalkedAsync(play, connection, 1);

        var back = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 2));
        var used = Enumerable.Range(0, 8).Select(direction => MapGeometry.Neighbour(TestPlay.NeighboursElementCell, direction));

        Assert.Contains(back.KeyMovements[^1] & 0xFFF, used);
        await TestWalks.WalkedAsync(play, connection, 2);
        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = NorthTreeInstanceId, ElementId = TestPlay.NeighboursElementId }, await TestWalks.SentAsync(play, connection, 3));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    // The middle map as the server describes it, its tree harvested.
    private static MapInformationEvent MiddleMapHarvested(int cell)
    {
        var information = TestPlay.Information(cell);

        information.InteractiveElements[0] = TestPlay.Tree(enabled: false);

        return information;
    }

    // The zone holds the north and east maps only; the east one seen empty, the tour goes north.
    private static HarvestActivity OutOfTheMiddleMap(TestPlay play)
    {
        var sightings = new HarvestSightings();
        var zones = new TestPlaceZones().Add("woods", TestPlay.NorthMapId, TestPlay.EastMapId);

        sightings.Seen(TestPlay.EastMapId, [], DateTimeOffset.UnixEpoch);

        return new HarvestActivity(new HarvestSettings { Resources = [TestPlay.Ash], Place = HarvestPlace.Zone, Zone = "woods" }, play.World, play.Tables, play.Maps, sightings, new GraphicSkills(), play.Places, new Regrowths(), play.Time, zones: zones);
    }

    [Fact]
    public async Task AResourceGrowingBackOnAMapOutOfTheZoneTheTourCrossedAlreadyIsHarvestedThere()
    {
        // The middle map, out of the zone, is crossed once its nothing is taken (D439); its tree grows back as the walk north goes.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(MiddleMapHarvested(433));
        await bot.ProcessedAsync();

        var run = bot.StartActivity(OutOfTheMiddleMap(play));

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: true) });
        await TestWalks.WalkedAsync(play, connection, 1);

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 2));
        await TestWalks.WalkedAsync(play, connection, 2);
        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await TestWalks.SentAsync(play, connection, 3));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AResourceGrowingBackOnAMapTheJourneyCrossesHoldsItThere()
    {
        // From the east map, out of the zone, the way north crosses the middle map, its tree harvested as the character comes; it
        // grows back as the walk to the north edge goes.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.EastMapId);
        var zones = new TestPlaceZones().Add("woods", TestPlay.NorthMapId);
        var activity = new HarvestActivity(new HarvestSettings { Resources = [TestPlay.Ash], Place = HarvestPlace.Zone, Zone = "woods" }, play.World, play.Tables, play.Maps, new HarvestSightings(), new GraphicSkills(), play.Places, new Regrowths(), play.Time, zones: zones);
        var run = bot.StartActivity(activity);

        await TestWalks.WalkedAsync(play, connection, 1);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Client(new MapInformationRequest { MapId = TestPlay.MapId });
        connection.Server(MiddleMapHarvested(265));

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: true) });
        await TestWalks.WalkedAsync(play, connection, 3);

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 4));
        await TestWalks.WalkedAsync(play, connection, 4);
        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await TestWalks.SentAsync(play, connection, 5));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AnElementTheProfileDoesNotWantGrowingBackDoesNotHoldTheJourney()
    {
        await using var play = new TestPlay();
        var (connection, run) = await LeavingTheNorthMapAsync(play, TestPlay.ChestnutSkillId);

        connection.Server(new InteractiveElementUpdatedEvent { Element = NorthTree(enabled: true, TestPlay.ChestnutSkillId) });
        await TestWalks.WalkedAsync(play, connection, 1);

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }
}
