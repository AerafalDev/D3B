using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fighting;
using D3B.Bot.Fights;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Monsters;
using D3B.Data.World;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Fighting;

/// <summary>The fighting activity (D236): which groups it attacks, where it hunts, and the attack from the group's own cell, measured (9g).</summary>
public sealed class FightActivityTests : IAsyncDisposable
{
    private const int Larva = 489;
    private const int Bouftou = 101;
    private const long GroupId = -3;
    private const int GroupCell = 412;

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static MapMonsterGroup Group(int? cell, params (int Monster, int Level)[] monsters)
    {
        return new MapMonsterGroup(GroupId, cell, [.. monsters.Select(monster => new MonsterInGroup(monster.Monster, 1, monster.Level))]);
    }

    private static Actor GroupActor(long actorId, int cell, params (int Monster, int Level)[] monsters)
    {
        var group = new MonsterGroup { Main = new GroupMonster { MonsterId = monsters[0].Monster, Grade = 1, Level = monsters[0].Level } };

        group.Others.AddRange(monsters.Skip(1).Select(monster => new GroupMonster { MonsterId = monster.Monster, Grade = 1, Level = monster.Level }));

        return new Actor
        {
            Id = actorId,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation { Roleplay = new RoleplayActor { MonsterGroup = new MonsterGroupActor { Monsters = group } } },
        };
    }

    private static FightEnd Ended(FightOutcome outcome)
    {
        return new FightEnd(outcome, 0, 0, ImmutableSortedDictionary<int, int>.Empty);
    }

    private FightActivity Activity(FightSettings settings)
    {
        return new FightActivity(settings, _play.World, _play.Tables, _play.Maps);
    }

    [Fact]
    public void AGroupIsAttackedWithinItsSizeNoMonsterLeftOutAndAsTheModeSays()
    {
        var automatic = new FightSettings { LeastMonsters = 2, MostMonsters = 3, Excluded = [Bouftou] };
        var chosen = new FightSettings { Mode = FightMode.Chosen, Monsters = [Larva] };

        Assert.True(GroupJudge.Allows(automatic, Group(GroupCell, (Larva, 20), (Larva, 30)), level: 30, _play.Tables.Monsters));
        Assert.False(GroupJudge.Allows(automatic, Group(GroupCell, (Larva, 20), (Larva, 31)), level: 30, _play.Tables.Monsters));
        Assert.False(GroupJudge.Allows(automatic, Group(GroupCell, (Larva, 20)), level: 30, _play.Tables.Monsters));
        Assert.False(GroupJudge.Allows(automatic, Group(GroupCell, (Larva, 20), (Bouftou, 10)), level: 30, _play.Tables.Monsters));
        Assert.False(GroupJudge.Allows(automatic, Group(null, (Larva, 20), (Larva, 20)), level: 30, _play.Tables.Monsters));
        Assert.True(GroupJudge.Allows(chosen, Group(GroupCell, (Bouftou, 90), (Larva, 90)), level: 30, _play.Tables.Monsters));
        Assert.False(GroupJudge.Allows(chosen, Group(GroupCell, (Bouftou, 10)), level: 30, _play.Tables.Monsters));
    }

    [Fact]
    public void AGroupHoldingAMonsterOfAFamilyChosenIsAttackedAsOneHoldingAMonsterChosen()
    {
        // The bouftou is of family 10, the larva of family 20: their races, which super races 1 and 2 group.
        var monsters = new MonsterCatalog(
            new Monster[] { new() { Id = Bouftou, Race = 10, FirstGrade = 0, GradeCount = 1 }, new() { Id = Larva, Race = 20, FirstGrade = 1, GradeCount = 1 } },
            new MonsterGrade[] { new() { Grade = 1, Level = 10 }, new() { Grade = 1, Level = 10 } },
            Array.Empty<MonsterSpell>(),
            Array.Empty<byte>(),
            Array.Empty<MonsterRatio>(),
            new[] { new MonsterRace(10, 1, 0), new MonsterRace(20, 2, 0) },
            new[] { new MonsterRace(1, MonsterRace.NoSuperRace, 0), new MonsterRace(2, MonsterRace.NoSuperRace, 0) });
        var family = new FightSettings { Mode = FightMode.Chosen, Families = [20] };

        Assert.Equal(20, ChosenMonsters.FamilyOf(monsters, Larva));
        Assert.Null(ChosenMonsters.FamilyOf(monsters, 7));
        Assert.True(ChosenMonsters.Any(family));
        Assert.False(ChosenMonsters.Any(new FightSettings { Mode = FightMode.Chosen }));
        Assert.True(GroupJudge.Allows(family, Group(GroupCell, (Bouftou, 10), (Larva, 10)), level: 30, monsters));
        Assert.False(GroupJudge.Allows(family, Group(GroupCell, (Bouftou, 10)), level: 30, monsters));
    }

    [Fact]
    public void AMonsterIsWorthWholeAtTheCharactersLevelHalvedEachTwentyLevelsBelowAndNothingAbove()
    {
        MonsterGrade[] grades =
        [
            new() { Grade = 1, Level = 60 },
            new() { Grade = 1, Level = 10 },
            new() { Grade = 2, Level = 14 },
        ];
        var monsters = new MonsterCatalog(
            new Monster[] { new() { Id = Bouftou, FirstGrade = 0, GradeCount = 1 }, new() { Id = Larva, FirstGrade = 1, GradeCount = 2 } },
            grades,
            Array.Empty<MonsterSpell>(),
            Array.Empty<byte>(),
            Array.Empty<MonsterRatio>(),
            Array.Empty<MonsterRace>(),
            Array.Empty<MonsterRace>());

        Assert.Equal(1, HuntingGround.WorthOf(monsters, Larva, 12));
        Assert.Equal(0.5, HuntingGround.WorthOf(monsters, Larva, 34), 6);
        Assert.Equal(0, HuntingGround.WorthOf(monsters, Bouftou, 59));
        Assert.Equal(0, HuntingGround.WorthOf(monsters, 7, 100));
    }

    [Fact]
    public void TheAutomaticPlaceHoldsTenMapsOnAWorldMapElseItIsTheRichestOfTheOthers()
    {
        const int den = 30;
        const int plain = 31;
        const int tofu = 98;
        MonsterGrade[] grades = [new() { Grade = 1, Level = 5 }, new() { Grade = 1, Level = 10 }];
        var monsters = new MonsterCatalog(
            new Monster[] { new() { Id = tofu, FirstGrade = 0, GradeCount = 1 }, new() { Id = Larva, FirstGrade = 1, GradeCount = 1 } },
            grades,
            Array.Empty<MonsterSpell>(),
            Array.Empty<byte>(),
            Array.Empty<MonsterRatio>(),
            Array.Empty<MonsterRace>(),
            Array.Empty<MonsterRace>());

        HuntingGround GroundOf(int plainMaps)
        {
            // The den, one map on no world map, holds both monsters, worth the most; the plain its larvae alone.
            MapPosition[] positions = [new(1, 0, 0, den, -1, 0, 0), .. Enumerable.Range(0, plainMaps).Select(index => new MapPosition(10 + index, (short)index, 0, plain, 1, 0, 0))];
            var graph = new WorldGraph(positions.Select(position => new WorldVertex(position.MapId, 1)).ToArray(), Array.Empty<WorldEdge>(), Array.Empty<WorldTransition>(), Array.Empty<string>());
            var atlas = new WorldAtlas(
                positions,
                new[] { new SubArea(den, 0, 1, 1, SubArea.NoZaap, true, 0, 0, 0, 2), new SubArea(plain, 0, 1, 1, SubArea.NoZaap, true, 0, 0, 2, 1) },
                new[] { new Area(1, 0, 0) },
                Array.Empty<Waypoint>(),
                Array.Empty<int>(),
                new[] { Larva, tofu, Larva });

            return HuntingGround.Of(new FightSettings(), atlas, graph, monsters, 12, subscribed: true);
        }

        Assert.Equal([plain], GroundOf(HuntingGround.LeastMapsShown).SubAreas);
        Assert.Equal([den], GroundOf(HuntingGround.LeastMapsShown - 1).SubAreas);
    }

    [Fact]
    public void AMapOfTheGroundReachedOnlyFarRoundIsLeftOutUnlessTheSettingsTakeIt()
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
            new[] { new SubArea(forest, 0, 1, 1, SubArea.NoZaap, true, 0, 0, 0, 1), new SubArea(other, 0, 1, 1, SubArea.NoZaap, true, 0, 0, 1, 0) },
            new[] { new Area(1, 0, 0) },
            Array.Empty<Waypoint>(),
            Array.Empty<int>(),
            new[] { Larva });
        var settings = new FightSettings { Place = FightPlace.Chosen, SubAreas = [forest] };
        var ground = HuntingGround.Of(settings, atlas, graph, _play.Tables.Monsters, 50, subscribed: true);

        Assert.Equal([9L], ground.Isolated);
        Assert.Equal([1L, 2L, 3L, 5L], ground.Maps.Keys.Order());

        settings.IsolatedMaps = true;
        ground = HuntingGround.Of(settings, atlas, graph, _play.Tables.Monsters, 50, subscribed: true);

        // Taken, it is hunted on, the activity travelling to it last (D149).
        Assert.Equal([9L], ground.Isolated);
        Assert.Equal([1L, 2L, 3L, 5L, 9L], ground.Maps.Keys.Order());
    }

    [Fact]
    public void AGroundChosenIsTheMapsOfItsSubAreasTheWorldReaches()
    {
        var ground = HuntingGround.Of(new FightSettings { Place = FightPlace.Chosen, SubAreas = [TestPlay.SubAreaId] }, _play.World.Atlas, _play.World.Graph, _play.Tables.Monsters, 50, subscribed: true);

        Assert.Equal([TestPlay.SubAreaId], ground.SubAreas);
        Assert.Contains(TestPlay.MapId, ground.Maps.Keys);
        Assert.All(ground.Maps.Values, subArea => Assert.Equal(TestPlay.SubAreaId, subArea));
    }

    [Fact]
    public async Task TheCharacterWalksOntoTheGroupsCellThenAttacksIt()
    {
        var (connection, bot) = await _play.EnterAsync(433);

        connection.Server(new MapActorShownEvent { Actors = { GroupActor(GroupId, GroupCell, (Larva, 20)) } });
        await bot.ProcessedAsync();

        var activity = Activity(new FightSettings
        {
            Mode = FightMode.Chosen,
            Monsters = [Larva],
            OnlyWinnable = false,
            Place = FightPlace.Chosen,
            SubAreas = [TestPlay.SubAreaId],
            Heal = false,
        });
        var run = bot.StartActivity(activity);

        Assert.Equal(GroupCell, await TestWalks.WalkedAsync(_play, connection, 1));
        Assert.Equal(new AttackMonsterRequest { MonsterGroupId = GroupId }, await connection.SentAsync(2));
        Assert.Equal(GroupCell, bot.State.Cell);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AGroupTheServerAnswersNothingForIsLeftAndTheTourGoesOn()
    {
        var (connection, bot) = await _play.EnterAsync(GroupCell);

        connection.Server(new MapActorShownEvent { Actors = { GroupActor(GroupId, GroupCell, (Larva, 20)) } });
        await bot.ProcessedAsync();

        var activity = Activity(new FightSettings { OnlyWinnable = false, Place = FightPlace.Chosen, SubAreas = [TestPlay.SubAreaId], Heal = false });
        var run = bot.StartActivity(activity);

        // Standing on the group's cell already, as measured after a fight: the attack is sent without a walk.
        Assert.Equal(new AttackMonsterRequest { MonsterGroupId = GroupId }, await connection.SentAsync(1));

        // No answer: the group is left for the visit, and the map gone through.
        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(_play, connection, 2));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public void TheActivityFightsItsOwnFightsAndCountsWonAndLost()
    {
        var activity = Activity(new FightSettings());

        Assert.True(activity.FightsItsFights);
        Assert.Equal(InterruptionResponse.Resume, activity.OnFightOver(Ended(FightOutcome.Won), InterruptionResponse.Resume));
        Assert.Equal(InterruptionResponse.Resume, activity.OnFightOver(Ended(FightOutcome.Lost), InterruptionResponse.Resume));
        Assert.Equal(InterruptionResponse.Resume, activity.OnFightOver(Ended(FightOutcome.Lost), InterruptionResponse.Resume));

        // Three lost in a row stop it, as harvesting (D198).
        Assert.Equal(InterruptionResponse.Stop, activity.OnFightOver(Ended(FightOutcome.Lost), InterruptionResponse.Resume));
        // Counted: one fight won, three lost.
        Assert.Equal(["1 fight won", "3 fights lost"], activity.Report!.Counts.Select(count => string.Format(System.Globalization.CultureInfo.InvariantCulture, (count.Text.Many is { } many && count.Text.Count != 1 ? many : count.Text.Format).In(System.Globalization.CultureInfo.InvariantCulture), [.. count.Text.Values.Select(value => value.Text ?? value.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))])));
    }
}
