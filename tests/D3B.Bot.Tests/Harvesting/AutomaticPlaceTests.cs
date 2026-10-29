using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Harvesting;

public sealed class AutomaticPlaceTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private IReadOnlyList<PlaceCandidate> Rank(HarvestSettings settings, int level, int lumberjack, GraphicSkills? graphics = null)
    {
        return AutomaticPlace.Rank(
            HarvestZone.TargetsOf(settings, _play.Tables.Jobs).Values,
            level,
            new Dictionary<int, int> { [TestPlay.Lumberjack] = lumberjack },
            settings.SafeLevel,
            _play.World.Atlas,
            _play.World.Graph,
            _play.Places,
            graphics ?? new GraphicSkills());
    }

    private static HarvestSettings Lumberjack(bool safeLevel = true)
    {
        return new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Place = HarvestPlace.Automatic, SafeLevel = safeLevel };
    }

    [Fact]
    public void WhileNoGraphicIsLearnedASubAreaCountsTheWorthOfTheResourcesItLists()
    {
        var first = Assert.Single(Rank(Lumberjack(), 50, 1));
        var twenty = Assert.Single(Rank(Lumberjack(), 50, 20));

        // At its profession's first level, the ash alone, worth one; at the 20th, the chestnut too, the ash 19 levels below.
        Assert.Equal(TestPlay.SubAreaId, first.SubAreaId);
        Assert.Equal(1, first.Score, 3);
        Assert.Equal(1 + Math.Pow(2, -19.0 / AutomaticPlace.HalvingLevels), twenty.Score, 3);
        Assert.Equal(_play.World.Atlas.Positions.ToArray().Count(position => position.SubAreaId == TestPlay.SubAreaId && _play.World.Graph.Contains(position.MapId)), first.Maps);
    }

    [Fact]
    public void EachMapTheLearnedGraphicsPlaceAResourceOnCountsItsWorth()
    {
        var graphics = new GraphicSkills();

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        var placed = _play.Places.MapsOffering(new HashSet<int> { TestPlay.TreeSkillId }, graphics).Count;

        Assert.True(placed > 0);
        Assert.Equal(placed, Assert.Single(Rank(Lumberjack(), 50, 1, graphics)).Score, 3);
    }

    [Fact]
    public void ASubAreaAboveTheCharactersLevelIsLeftOutWhenTheSettingsSaySo()
    {
        Assert.Empty(Rank(Lumberjack(), 5, 1));
        Assert.Single(Rank(Lumberjack(safeLevel: false), 5, 1));
        Assert.Single(Rank(Lumberjack(), 0, 1));
    }

    [Fact]
    public void NothingTheCharacterCanHarvestAtItsLevelsLeavesNoPlace()
    {
        Assert.Empty(Rank(new HarvestSettings { Resources = [TestPlay.Chestnut], Place = HarvestPlace.Automatic }, 50, 1));
    }
}
