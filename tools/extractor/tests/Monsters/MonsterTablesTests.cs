using D3B.Extractor.Code;
using D3B.Extractor.Monsters;
using D3B.Extractor.Tests.Unity;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Monsters;

public sealed class MonsterTablesTests
{
    private static readonly Text _larva = new() { Fr = "Larve Bleue", En = "Blue Larva", De = "Blaue Larve", Es = "Larva Azul", Pt = "Larva Azul" };
    private static readonly Text _wabbit = new() { Fr = "Wabbit", En = "Wabbit", De = "Wabbit", Es = "Wabbit", Pt = "Wabbit" };
    private static readonly Text _various = new() { Fr = "Divers", En = "Miscellaneous", De = "Verschiedenes", Es = "Varios", Pt = "Diversos" };
    private static readonly Text _larvae = new() { Fr = "Larves", En = "Larvae", De = "Larven", Es = "Larvas", Pt = "Larvas" };
    private static readonly Text _fields = new() { Fr = "Créatures des champs", En = "Field creatures", De = "Feldkreaturen", Es = "Criaturas de los campos", Pt = "Criaturas dos campos" };

    private static readonly Monster _blueLarva = new()
    {
        Id = 31,
        Name = _larva,
        RaceId = 11,
        Flags = ["useSummonSlot", "canPlay"],
        Levels = [16, 17, 18, 19, 20],
        Drops =
        [
            new MonsterDrop { ItemId = 289, Chances = [55, 55, 55, 55, 55], Criterion = "Pj>41,9&CU>0" },
            new MonsterDrop { ItemId = 303, Chances = [9.24f, 9.5f, 10, 10.5f, 11], Criterion = null },
        ],
    };

    private static readonly Monster _wabbitMonster = new() { Id = 47, Name = _wabbit, RaceId = -1, Flags = [], Levels = [80, 75, 130], Drops = [] };
    private static readonly MonsterRace[] _races = [new() { Id = -1, Name = _various, SuperRaceId = 1 }, new() { Id = 11, Name = _larvae, SuperRaceId = 2 }];
    private static readonly MonsterSuperRace[] _superRaces = [new() { Id = 1, Name = _various }, new() { Id = 2, Name = _fields }];
    private static readonly HashSet<int> _items = [303, 289];

    [Fact]
    public void MonstersTheirRacesAndSuperRacesAreWrittenAsTheAppReadsThem()
    {
        // The app's own tests read the same fixtures (src-tauri/src/data/monsters/).
        Assert.Equal(Fixture("monsters.json"), MonsterTables.WriteMonsters([_blueLarva, _wabbitMonster]));
        Assert.Equal(Fixture("monster-races.json"), MonsterTables.WriteRaces(_races));
        Assert.Equal(Fixture("monster-super-races.json"), MonsterTables.WriteSuperRaces(_superRaces));
    }

    [Fact]
    public void GradesNumberedFromOneInOrderGiveTheirLevels()
    {
        // Wabbit Gm's levels fall from grade 5 to 6 (182 on 3.7.13.17): written as the client holds them.
        Assert.Equal([80, 75, 130], MonsterTables.LevelsOf(182, [(1, 182, 80), (2, 182, 75), (3, 182, 130)]));
    }

    [Fact]
    public void GradesOutOfOrderOfAnotherMonsterOrNoneAreRefused()
    {
        var order = Assert.Throws<InvalidDataException>(() => MonsterTables.LevelsOf(31, [(2, 31, 16), (1, 31, 17)]));
        var other = Assert.Throws<InvalidDataException>(() => MonsterTables.LevelsOf(31, [(1, 34, 16)]));
        var none = Assert.Throws<InvalidDataException>(() => MonsterTables.LevelsOf(31, []));

        Assert.Equal("Monster 31's grade 1 is numbered 2.", order.Message);
        Assert.Equal("Monster 31's grade 1 is of monster 34.", other.Message);
        Assert.Equal("Monster 31 has no grade.", none.Message);
    }

    [Fact]
    public void ADropKeepsItsItemItsChancesAndItsCriterionOrNone()
    {
        var meat = MonsterTables.DropOf(31, 31, 289, [55, 55, 55, 55, 55], "Pj>41,9&CU>0", true);
        var wing = MonsterTables.DropOf(31, 31, 303, [9.24f, 9.5f, 10, 10.5f, 11], "", false);

        Assert.Equal((289, "Pj>41,9&CU>0"), (meat.ItemId, meat.Criterion));
        Assert.Equal([9.24f, 9.5f, 10, 10.5f, 11], wing.Chances);
        Assert.Null(wing.Criterion);
    }

    [Fact]
    public void ADropOfAnotherMonsterOutOfPercentOrWhoseCriterionContradictsItsFlagIsRefused()
    {
        var other = Assert.Throws<InvalidDataException>(() => MonsterTables.DropOf(31, 34, 303, [1, 1, 1, 1, 1], "", false));
        var percent = Assert.Throws<InvalidDataException>(() => MonsterTables.DropOf(31, 31, 303, [1, 1, 101, 1, 1], "", false));
        var negative = Assert.Throws<InvalidDataException>(() => MonsterTables.DropOf(31, 31, 303, [-1, 1, 1, 1, 1], "", false));
        var flagged = Assert.Throws<InvalidDataException>(() => MonsterTables.DropOf(31, 31, 303, [1, 1, 1, 1, 1], "", true));
        var unflagged = Assert.Throws<InvalidDataException>(() => MonsterTables.DropOf(31, 31, 303, [1, 1, 1, 1, 1], "Qa=1198", false));

        Assert.Equal("Monster 31 lists a drop of monster 34.", other.Message);
        Assert.Equal("Monster 31 drops item 303 at 101 % for grade 3.", percent.Message);
        Assert.Contains("at -1 %", negative.Message, StringComparison.Ordinal);
        Assert.Equal("Monster 31's drop of item 303 has a criterion '' where its flag says True.", flagged.Message);
        Assert.Contains("'Qa=1198'", unflagged.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TablesWhoseReferencesHoldAreKeptWhole()
    {
        var (monsters, races, skippedRaces, skippedMonsters) = MonsterTables.Kept([_blueLarva, _wabbitMonster], _races, _superRaces, _items, [[31, 47], []]);

        Assert.Equal([31, 47], monsters.Select(monster => monster.Id));
        Assert.Equal((2, 0, 0), (races.Count, skippedRaces, skippedMonsters));
    }

    [Fact]
    public void ARaceOfASuperRaceTheClientLacksIsLeftOutWithItsMonsters()
    {
        // Race 347 names super race 50 on 3.7.13.17, and no sub-area lists its 6 monsters (the owner, 2026-11-17).
        var (monsters, races, skippedRaces, skippedMonsters) = MonsterTables.Kept([_blueLarva, _wabbitMonster], _races, [_superRaces[0]], _items, [[47]]);

        Assert.Equal(47, Assert.Single(monsters).Id);
        Assert.Equal((-1, 1, 1), (Assert.Single(races).Id, skippedRaces, skippedMonsters));
    }

    [Fact]
    public void AReferenceTheTablesLackIsRefused()
    {
        var race = Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva], [_races[0]], _superRaces, _items, []));
        var item = Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva], _races, _superRaces, [303], []));
        var subArea = Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva], _races, _superRaces, _items, [[31, 182]]));
        var leftOut = Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva], _races, [_superRaces[0]], _items, [[31]]));
        var negative = Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva], _races, _superRaces, _items, [[-1]]));

        Assert.Equal("Monster 31 is of race 11, which the client lacks.", race.Message);
        Assert.Equal("Monster 31 drops item 289, which items.json lacks.", item.Message);
        Assert.Equal("A sub-area names monster 182, which monsters.json lacks.", subArea.Message);
        Assert.Equal("A sub-area names monster 31, which monsters.json lacks.", leftOut.Message);
        Assert.Equal("A sub-area names monster -1, which monsters.json lacks.", negative.Message);
    }

    [Fact]
    public void AnIdHeldTwiceInAnyOfTheThreeTablesIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva, _blueLarva], _races, _superRaces, _items, []));
        Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva], [.. _races, _races[1]], _superRaces, _items, []));
        Assert.Throws<InvalidDataException>(() => MonsterTables.Kept([_blueLarva], _races, [.. _superRaces, _superRaces[0]], _items, []));
    }

    [Fact]
    public void ARaceIsReadFieldByFieldAndRefusedWithoutAName()
    {
        var tree = TypeTree.Build("MonsterRaceData", [Node(0, "MonsterRaceData", "Base"), Node(1, "int", "id", 4), Node(1, "int", "superRaceId", 4), Node(1, "int", "nameId", 4)]);
        var bytes = new ObjectBytes();

        bytes.Int32(11);
        bytes.Int32(2);
        bytes.Int32(1112421);

        var data = bytes.ToArray();
        var reader = new ObjectReader(data, tree);
        var record = Fields.Read(ref reader, 0, tree.Name);
        var race = Assert.Single(MonsterTables.ReadRaces([record], id => id == 1112421 ? _larvae : null));

        Assert.Equal((11, "Larves", 2), (race.Id, race.Name.Fr, race.SuperRaceId));
        Assert.Throws<InvalidDataException>(() => MonsterTables.ReadRaces([record], _ => null));
    }

    [Fact]
    public void AMonsterIsReadFieldByFieldWithItsGradesAndDrops()
    {
        var flags = FlagEnum.From("MonsterFlags", [("UseSummonSlot", 1), ("IsBoss", 4), ("CanPlay", 64)]);
        var monster = Assert.Single(MonsterTables.ReadMonsters([MonsterRecord(sixthChance: false)], id => id == 6522 ? _larva : null, flags));

        Assert.Equal((31, "Larve Bleue", 11), (monster.Id, monster.Name.Fr, monster.RaceId));
        Assert.Equal(["useSummonSlot", "canPlay"], monster.Flags);
        Assert.Equal([16, 17], monster.Levels);

        var drop = Assert.Single(monster.Drops);

        Assert.Equal((303, "Qa=1198"), (drop.ItemId, drop.Criterion));
        Assert.Equal([1, 2, 3, 4, 5.5f], drop.Chances);
    }

    [Fact]
    public void ADropGivingChancesForASixthGradeIsRefused()
    {
        var flags = FlagEnum.From("MonsterFlags", [("UseSummonSlot", 1), ("CanPlay", 64)]);
        var failure = Assert.Throws<InvalidDataException>(() => MonsterTables.ReadMonsters([MonsterRecord(sixthChance: true)], _ => _larva, flags));

        Assert.Equal("Monster 31's drops give chances for more than 5 grades.", failure.Message);
    }

    [Fact]
    public void TheInstalledClientsMonstersAreAsMeasured()
    {
        var client = Installed.Client("3.7.13.17");
        var texts = TextFiles.Open(client);

        using var code = ClientCode.Open(client);

        var monsters = MonsterTables.ReadMonsters(DataTable.ReadById(client, "monsters"), texts.Name, code.FlagEnum("MonsterFlags"));
        var races = MonsterTables.ReadRaces(DataTable.ReadById(client, "monsterraces"), texts.Name);
        var superRaces = MonsterTables.ReadSuperRaces(DataTable.ReadById(client, "monstersuperraces"), texts.Name);
        var drops = monsters.SelectMany(monster => monster.Drops).ToList();

        // 3.7.13.17, measured for format 5 (docs/data.md): 5,135 monsters of 26,851 grades, levels 1 to 2,400; 2,348 drop
        // 3,513 items, 13,567 drops, 8,645 under a criterion, 1,374 a hunter's (job 41); 266 races, 35 super races; race
        // 347 of super race 50, which the client lacks, left out with its 6 monsters.
        Assert.Equal((5135, 26851, 266, 35), (monsters.Count, monsters.Sum(monster => monster.Levels.Count), races.Count, superRaces.Count));
        Assert.Equal((1, 2400), (monsters.Min(monster => monster.Levels.Min()), monsters.Max(monster => monster.Levels.Max())));
        Assert.Equal((2348, 3513, 13567), (monsters.Count(monster => monster.Drops.Count > 0), drops.Select(drop => drop.ItemId).Distinct().Count(), drops.Count));
        Assert.Equal((8645, 1374), (drops.Count(drop => drop.Criterion is not null), drops.Count(drop => drop.Criterion?.Contains("Pj>41,", StringComparison.Ordinal) is true)));
        Assert.Equal((204, 306), (monsters.Count(monster => monster.Flags.Contains("isBoss")), monsters.Count(monster => monster.Flags.Contains("isMiniBoss"))));
        Assert.Equal([80, 80, 80, 80, 80, 75, 130, 200], monsters.Single(monster => monster.Id == 182).Levels);

        var subAreas = DataTable.ReadById(client, "subareas").Select(record => record.Int32Array("monsters"));
        var items = DataTable.ReadById(client, "items").Select(item => item.Int32("id")).ToList();
        var (kept, keptRaces, skippedRaces, skippedMonsters) = MonsterTables.Kept(monsters, races, superRaces, items, subAreas);

        Assert.Equal((5129, 265, 1, 6), (kept.Count, keptRaces.Count, skippedRaces, skippedMonsters));
        Assert.DoesNotContain(keptRaces, race => race.Id == 347);
    }

    // A monster's record as the client lays it out (3.7.13.17), with two grades and one drop, its other fields left out.
    private static Fields MonsterRecord(bool sixthChance)
    {
        int[] chances = sixthChance ? [1, 2, 3, 4, 5, 6] : [1, 2, 3, 4, 5];
        var tree = TypeTree.Build("MonsterData",
        [
            Node(0, "MonsterData", "Base"),
            Node(1, "int", "m_flags", 4),
            Node(1, "UInt16", "id", 2),
            Node(1, "SInt16", "race", 2),
            Node(1, "int", "nameId", 4),
            Node(1, "vector", "grades"),
            Array(2),
            Node(3, "int", "size", 4),
            Node(3, "MonsterGrade", "data"),
            Node(4, "int", "grade", 4),
            Node(4, "UInt16", "monsterId", 2),
            Node(4, "UInt16", "level", 2),
            Node(1, "vector", "drops"),
            Array(2),
            Node(3, "int", "size", 4),
            Node(3, "MonsterDrop", "data"),
            Node(4, "int", "monsterId", 4),
            Node(4, "int", "objectId", 4),
            .. chances.Select(grade => Node(4, "float", $"percentDropForGrade{grade}", 4)),
            .. String(4, "criterions"),
            Node(4, "UInt8", "hasCriterions", 1, align: true),
        ]);
        var bytes = new ObjectBytes();

        bytes.Int32(65);
        bytes.Raw([.. BitConverter.GetBytes((ushort)31), .. BitConverter.GetBytes((short)11)]);
        bytes.Int32(6522);
        bytes.Int32(2);

        foreach (var (grade, level) in (ReadOnlySpan<(int, ushort)>)[(1, 16), (2, 17)])
        {
            bytes.Int32(grade);
            bytes.Raw([.. BitConverter.GetBytes((ushort)31), .. BitConverter.GetBytes(level)]);
        }

        bytes.Int32(1);
        bytes.Int32(31);
        bytes.Int32(303);

        foreach (var chance in chances)
        {
            bytes.Single(chance is 5 ? 5.5f : chance);
        }

        bytes.String("Qa=1198");
        bytes.Byte(1);
        bytes.Align();

        var data = bytes.ToArray();
        var reader = new ObjectReader(data, tree);
        var fields = Fields.Read(ref reader, 0, tree.Name);

        reader.ExpectEnd();

        return fields;
    }

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "monsters", name));
    }
}
