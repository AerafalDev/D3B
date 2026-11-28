using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Data.World;

namespace D3B.Bot.Tests.Harvesting;

public sealed class AutomaticPlaceTests : IAsyncDisposable
{
    private const int Forest = 10;
    private const int Grove = 20;

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
            settings.IsolatedMaps,
            _play.World.Atlas,
            _play.World.Graph,
            _play.Places,
            graphics ?? new GraphicSkills());
    }

    private static HarvestSettings Lumberjack(bool safeLevel = true)
    {
        return new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Place = HarvestPlace.Automatic, SafeLevel = safeLevel };
    }

    // The forest's maps 1 to 3 hold together, map 9 lies five changes away through the grove's 4, 6, 7 and 8; when not listed,
    // the tree is drawn on map 9 alone of the forest, and on the grove's map 4.
    private static SmallWorld IsolatedForest(bool listed)
    {
        long[] ids = [1, 2, 3, 4, 6, 7, 8, 9];
        int[] subAreaOf = [Forest, Forest, Forest, Grove, Grove, Grove, Grove, Forest];
        (int From, int To)[] links = [(0, 1), (1, 0), (1, 2), (2, 1), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7)];

        return new SmallWorld(
            [.. ids.Select((id, index) => (id, subAreaOf[index]))],
            listed ? [(Forest, [TestPlay.Ash]), (Grove, [])] : [(Forest, []), (Grove, [])],
            drawn: listed ? [] : [4, 9],
            links: links);
    }

    [Fact]
    public void AResourceNoLearnedGraphicPlacesCountsOnAShareOfTheMapsOfEachSubAreaListingIt()
    {
        // D586: the forest's ten maps and the grove's four list the ash, which no graphic learned places anywhere; the median
        // learned resource grows on 17 % of the maps of the sub-areas listing it.
        using var world = new SmallWorld([.. Enumerable.Range(1, 14).Select(id => ((long)id, id <= 10 ? Forest : Grove))], [(Forest, [TestPlay.Ash]), (Grove, [TestPlay.Ash])]);

        var ranked = world.Rank(new GraphicSkills(), isolatedMaps: false);

        Assert.Equal([Forest, Grove], ranked.Select(candidate => candidate.SubAreaId));
        Assert.Equal(1.7, ranked[0].Score, 3);
        Assert.Equal(0.68, ranked[1].Score, 3);
        Assert.Equal(10, ranked[0].Maps);
    }

    [Fact]
    public void AResourceAGraphicPlacesSomewhereCountsOnlyWhereItIsDrawn()
    {
        // The tree is drawn on the grove's map 12 alone: the forest lists the ash, which no longer counts there.
        using var world = new SmallWorld([.. Enumerable.Range(1, 14).Select(id => ((long)id, id <= 10 ? Forest : Grove))], [(Forest, [TestPlay.Ash]), (Grove, [TestPlay.Ash])], drawn: [12]);
        var graphics = new GraphicSkills();

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        var ranked = Assert.Single(world.Rank(graphics, isolatedMaps: false));

        Assert.Equal(Grove, ranked.SubAreaId);
        Assert.Equal(1, ranked.Score, 3);
    }

    [Fact]
    public void ASubAreaListingMoreResourcesThanAnyPlayedOneCountsNoEstimate()
    {
        // Measured on 3.7.14.18 (D586): the test sub-area lists 85 resources, the others 23 at most.
        int[] many = [TestPlay.Ash, .. Enumerable.Range(1, AutomaticPlace.MostListed)];
        using var world = new SmallWorld([(1, Forest), (2, Forest), (3, Grove)], [(Forest, many), (Grove, [TestPlay.Ash])]);

        Assert.Equal([Grove], world.Rank(new GraphicSkills(), isolatedMaps: false).Select(candidate => candidate.SubAreaId));
    }

    [Fact]
    public void AnEstimateCountsNoIsolatedMapUnlessTheTourTakesIt()
    {
        using var world = IsolatedForest(listed: true);

        Assert.Equal(0.51, Assert.Single(world.Rank(new GraphicSkills(), isolatedMaps: false)).Score, 3);
        Assert.Equal(0.68, Assert.Single(world.Rank(new GraphicSkills(), isolatedMaps: true)).Score, 3);
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
    public void AnIsolatedMapCountsForNothingUnlessTheTourTakesIt()
    {
        using var world = IsolatedForest(listed: false);
        var graphics = new GraphicSkills();

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        Assert.Equal([Grove], world.Rank(graphics, isolatedMaps: false).Select(candidate => candidate.SubAreaId));
        Assert.Equal([Forest, Grove], world.Rank(graphics, isolatedMaps: true).Select(candidate => candidate.SubAreaId));
    }

    [Fact]
    public void EachProfessionsTopIsTheSubAreaDrawingItsHighestLevelResourceOnTheMostMaps()
    {
        var graphics = new GraphicSkills();
        var settings = Lumberjack(safeLevel: false);
        var targets = HarvestZone.TargetsOf(settings, _play.Tables.Jobs).Values;
        var levels = new Dictionary<int, int> { [TestPlay.Lumberjack] = 50 };

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        var ranking = AutomaticPlace.Reckon(targets, 50, levels, false, false, _play.World.Atlas, _play.World.Graph, _play.Places, graphics);

        Assert.Equal(TestPlay.SubAreaId, ranking.Tops[TestPlay.Lumberjack]);
        Assert.Equal(AutomaticPlace.Rank(targets, 50, levels, false, false, _play.World.Atlas, _play.World.Graph, _play.Places, graphics), ranking.Candidates);
    }

    [Fact]
    public void NothingTheCharacterCanHarvestAtItsLevelsLeavesNoPlace()
    {
        Assert.Empty(Rank(new HarvestSettings { Resources = [TestPlay.Chestnut], Place = HarvestPlace.Automatic }, 50, 1));
    }

    [Fact]
    public void WhileNoGraphicPlacesItAProfessionsTopIsTheSubAreaListingItsHighestLevelResourceOnTheMostMaps()
    {
        using var world = new SmallWorld([.. Enumerable.Range(1, 14).Select(id => ((long)id, id <= 4 ? Forest : Grove))], [(Forest, [TestPlay.Ash]), (Grove, [TestPlay.Ash])]);

        Assert.Equal(Grove, world.Reckon(new GraphicSkills(), isolatedMaps: false).Tops[TestPlay.Lumberjack]);
    }

    /// <summary>A pack of a few maps, by default each linked to the next and back, in sub-areas that list resources.</summary>
    private sealed class SmallWorld : IDisposable
    {
        private readonly TemporaryDirectory _directory = new();
        private readonly TestPlay _play = new();
        private readonly GamePack _pack;
        private readonly WorldAtlas _atlas;
        private readonly WorldGraph _graph;
        private readonly GraphicPlaces _places;

        public SmallWorld((long Id, int SubAreaId)[] maps, (int SubAreaId, int[] Harvestables)[] subAreas, long[]? drawn = null, (int From, int To)[]? links = null)
        {
            var ids = maps.Select(map => map.Id).ToArray();

            links ??= [.. Enumerable.Range(0, ids.Length - 1).SelectMany(index => new[] { (index, index + 1), (index + 1, index) })];

            _graph = new WorldGraph(
                ids.Select(id => new WorldVertex(id, 1)).ToArray(),
                links.Select((link, index) => new WorldEdge(link.From, link.To, index, 1)).ToArray(),
                links.Select(link => new WorldTransition(WorldTransitionType.Scroll, 0, 0, WorldTransition.None, ids[link.To], WorldTransition.None, WorldTransition.None)).ToArray(),
                Array.Empty<string>());

            var harvestables = new List<int>();
            var areas = new List<SubArea>();

            foreach (var (subAreaId, listed) in subAreas)
            {
                areas.Add(new SubArea(subAreaId, 0, 1, 1, SubArea.NoZaap, true, harvestables.Count, listed.Length, 0, 0));
                harvestables.AddRange(listed);
            }

            _atlas = new WorldAtlas(
                maps.Select((map, index) => new MapPosition(map.Id, (short)index, 0, map.SubAreaId, 1, 0, 0)).ToArray(),
                areas.ToArray(),
                new[] { new Area(1, 0, 0) },
                Array.Empty<Waypoint>(),
                harvestables.ToArray(),
                Array.Empty<int>());

            var info = new PackInfo { ClientVersion = TestInstall.Version, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };
            var cells = new MapCell[MapData.CellCount];
            var path = _directory.Combine("data-3.pack");

            using (var stream = File.Create(path))
            using (var writer = new PackWriter(stream, info, 3))
            {
                writer.Add(WorldGraph.EntryName, _graph);
                writer.Add(WorldAtlas.EntryName, _atlas);

                foreach (var id in ids)
                {
                    MapInteractive[] interactives = drawn?.Contains(id) is true ? [new MapInteractive(1, 300, TestPlay.TreeGfxId)] : [];

                    writer.Add(MapData.EntryName(id), new MapData(id, 0, 0, 0, 0, cells, interactives));
                }

                writer.Complete();
            }

            _pack = GamePack.Open(path);
            _places = new GraphicPlaces(_pack, new GameWorld(_pack));
        }

        public IReadOnlyList<PlaceCandidate> Rank(GraphicSkills graphics, bool isolatedMaps)
        {
            return Reckon(graphics, isolatedMaps).Candidates;
        }

        public PlaceRanking Reckon(GraphicSkills graphics, bool isolatedMaps)
        {
            var settings = Lumberjack(safeLevel: false);

            return AutomaticPlace.Reckon(HarvestZone.TargetsOf(settings, _play.Tables.Jobs).Values, 50, new Dictionary<int, int> { [TestPlay.Lumberjack] = 1 }, false, isolatedMaps, _atlas, _graph, _places, graphics);
        }

        public void Dispose()
        {
            _pack.Dispose();
            _play.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _directory.Dispose();
        }
    }
}
