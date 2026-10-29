using D3B.Bot.Harvesting;
using D3B.Bot.Tests.Runtime;

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
        return HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs);
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
    public void EverythingKnownHarvestsEveryProfessionsResourceAlike()
    {
        var zone = ZoneOf(new HarvestSettings { EverythingKnown = true, Area = 1 });

        Assert.Equal([TestPlay.ChestnutSkillId, TestPlay.TreeSkillId], zone.Targets.Keys.Order());
        Assert.All(zone.Targets.Values, target => Assert.Equal(0, target.Priority));
        Assert.DoesNotContain(184, zone.Harvests.Keys);
    }
}
