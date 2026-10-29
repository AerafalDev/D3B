using D3B.Bot.Interactives;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Jobs;

namespace D3B.Bot.Tests.Settings;

public sealed class GameReferencesTests
{
    private readonly GameReferences _game = TestReferences.Game();

    [Fact]
    public void TheProfessionsAreEveryOneButTheBase()
    {
        Assert.Equal([new(TestReferences.Lumberjack, 202, null), new GameReference(TestReferences.Miner, 224, null)], _game.Options(SettingKind.Job));
        Assert.True(_game.Exists(SettingKind.Job, TestReferences.Miner));
        Assert.False(_game.Exists(SettingKind.Job, 1));
    }

    [Fact]
    public void SubAreasAreOfferedWithinTheirArea()
    {
        Assert.Equal([TestReferences.Amakna, TestReferences.Astrub], _game.Options(SettingKind.Area).Select(area => area.Id));
        Assert.Equal([TestReferences.Forest, TestReferences.Hills, TestReferences.City], _game.Options(SettingKind.SubArea).Select(subArea => subArea.Id));
        Assert.Equal([new(TestReferences.Forest, 110, 5), new GameReference(TestReferences.Hills, 111, 20)], _game.Options(SettingKind.SubArea, TestReferences.Amakna));
        Assert.True(_game.IsWithin(SettingKind.SubArea, TestReferences.City, TestReferences.Astrub));
        Assert.False(_game.IsWithin(SettingKind.SubArea, TestReferences.City, TestReferences.Amakna));
        Assert.False(_game.Exists(SettingKind.SubArea, 404));
    }

    [Fact]
    public void AProfessionsResourcesAreTheItemsItsSkillsGatherByLevel()
    {
        Assert.Equal(
            [new(TestReferences.Ash, 403, 1), new GameReference(TestReferences.Chestnut, 404, 10)],
            _game.Options(SettingKind.Resource, TestReferences.Lumberjack));
        Assert.Equal([TestReferences.Ash, TestReferences.Chestnut, TestReferences.Iron], _game.Options(SettingKind.Resource).Select(resource => resource.Id));
        Assert.True(_game.IsWithin(SettingKind.Resource, TestReferences.Iron, TestReferences.Miner));
        Assert.False(_game.IsWithin(SettingKind.Resource, TestReferences.Iron, TestReferences.Lumberjack));
        Assert.Empty(_game.Options(SettingKind.Resource, 404));
    }

    [Fact]
    public void TheSubAreasWhereResourcesGrowComeTheMostFirstThenTheLowest()
    {
        var holding = _game.SubAreasHolding([TestReferences.Chestnut, TestReferences.Ash]);

        Assert.Equal([TestReferences.Forest, TestReferences.Hills], holding.Select(subArea => subArea.SubArea.Id));
        Assert.Equal([TestReferences.Chestnut, TestReferences.Ash], holding[0].Resources);
        Assert.Equal([2, 1], holding.Select(subArea => subArea.Maps));
        Assert.Empty(_game.SubAreasHolding([TestReferences.Ash], TestReferences.Astrub));
        Assert.Equal([1_001L, 1_002L], _game.MapsOf(TestReferences.Forest));
    }

    [Fact]
    public void AResourcesSourceIsTheProfessionThatGathersItAtItsLowestLevel()
    {
        Assert.True(_game.TryGetSource(TestReferences.Chestnut, out var source));
        Assert.Equal(new ResourceSource(TestReferences.Lumberjack, 10), source);
        Assert.False(_game.TryGetSource(9_999, out _));
    }

    [Fact]
    public void OnlyTheGamesKindsHaveOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _game.Options(SettingKind.Number));
        Assert.False(_game.Exists(SettingKind.Flag, 1));
        Assert.False(_game.IsWithin(SettingKind.Area, TestReferences.Amakna, TestReferences.Amakna));
    }

    [Fact]
    public async Task BasesOwnResourcesAreOfferedWithoutALevelAndPlacedByTheLearnedGraphics()
    {
        const int well = 102;
        const int water = 311;
        await using var play = new TestPlay();
        var jobs = new JobCatalog(play.Tables.Jobs.Jobs, play.Tables.Jobs.Skills.ToArray().Append(new Skill(well, 0, Job.Base, water, 1, 1, false)).OrderBy(skill => skill.Id).ToArray());
        var graphics = new GraphicSkills();
        var game = new GameReferences(play.World.Atlas, jobs, play.Tables.Items, play.World.Graph, play.Places, graphics);

        Assert.Contains(game.Options(SettingKind.Resource), resource => resource.Id == water && resource.Level is null);
        Assert.True(game.Exists(SettingKind.Resource, water));
        Assert.True(game.IsBase(water));
        Assert.False(game.IsBase(TestPlay.Ash));
        Assert.False(game.TryGetSource(water, out _));
        Assert.Empty(game.PlacesOf(water)!);
        Assert.Null(game.PlacesOf(TestPlay.Ash));
        Assert.Empty(game.SubAreasHolding([water]));

        graphics.Learn(TestPlay.TreeGfxId, [well]);

        Assert.Contains(TestPlay.MapId, game.PlacesOf(water)!);
        Assert.Equal([TestPlay.SubAreaId], game.SubAreasHolding([water]).Select(subArea => subArea.SubArea.Id));
        Assert.Equal([water], game.SubAreasHolding([water]).Single().Resources);
    }
}
