using System.Globalization;
using D3B.Extractor.Code;
using D3B.Extractor.Items;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using D3B.Extractor.World;

namespace D3B.Extractor.Monsters;

/// <summary>The monsters of the data folder, their races and super races: <c>monsters.json</c>, <c>monster-races.json</c> and <c>monster-super-races.json</c>.</summary>
/// <remarks>
/// A monster keeps its grades' levels, its drops and its flags (the owner, 2026-11-17): what D3B's default blueprints
/// compute their ranges from, and Chasseur its meats, whose criterion names job 41. Its grades' characteristics, its
/// spells, its looks and its own sub-areas are left out: the sub-areas' <c>monsters</c> say where it lives.
/// </remarks>
internal static class MonsterTables
{
    /// <summary>The grades a drop gives chances for, <c>percentDropForGrade1</c> to <c>5</c>, whatever the monster's grades (3.7.13.17).</summary>
    public const int DropGrades = 5;

    /// <summary>Reads the monsters, their races and super races from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">The client's monsters are not what the extractor measured, or name a race the client lacks or an item <paramref name="items"/> lacks.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, ClientCode code, IReadOnlyList<Item> items, IReadOnlyList<SubArea> subAreas, DataFolder folder, TextWriter output)
    {
        var monsters = ReadMonsters(DataTable.ReadById(client, "monsters"), texts.Name, code.FlagEnum("MonsterFlags"));
        var races = ReadRaces(DataTable.ReadById(client, "monsterraces"), texts.Name);
        var superRaces = ReadSuperRaces(DataTable.ReadById(client, "monstersuperraces"), texts.Name);

        var (kept, keptRaces, skippedRaces, skippedMonsters) = Kept(monsters, races, superRaces, [.. items.Select(item => item.Id)], subAreas.Select(subArea => subArea.Monsters));

        folder.Write("monsters.json", WriteMonsters(kept));
        folder.Write("monster-races.json", WriteRaces(keptRaces));
        folder.Write("monster-super-races.json", WriteSuperRaces(superRaces));

        output.WriteLine($"Monsters: {kept.Count:N0} monsters, {kept.Sum(monster => monster.Levels.Count):N0} grades, {kept.Sum(monster => monster.Drops.Count):N0} drops; {keptRaces.Count:N0} races, {superRaces.Count:N0} super races; left out {skippedRaces:N0} races of a super race the client lacks and their {skippedMonsters:N0} monsters");
    }

    /// <exception cref="InvalidDataException">A monster names text 0, sets a flag its enum does not name, or its grades or drops are not what the extractor measured.</exception>
    public static IReadOnlyList<Monster> ReadMonsters(IEnumerable<Fields> records, Func<long, Text?> name, FlagEnum flags)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    int id = record.UInt16("id");

                    return new Monster
                    {
                        Id = id,
                        Name = ItemTables.Named("Monster", id, name(record.Int32("nameId"))),
                        RaceId = record.Int16("race"),
                        Flags = flags.Names((uint)record.Int32("m_flags")),
                        Levels = LevelsOf(id, [.. record.Items("grades").Select(grade => (grade.Int32("grade"), (int)grade.UInt16("monsterId"), (int)grade.UInt16("level")))]),
                        Drops = [.. record.Items("drops").Select(drop => ReadDrop(id, drop))],
                    };
                })
                .OrderBy(monster => monster.Id),
        ];
    }

    /// <summary>Gets the level of each grade, grade 1 first.</summary>
    /// <exception cref="InvalidDataException">The monster has no grade, or its grades are not numbered from 1 in order, or one is of another monster.</exception>
    public static IReadOnlyList<int> LevelsOf(int monster, IReadOnlyList<(int Grade, int MonsterId, int Level)> grades)
    {
        if (grades.Count is 0)
        {
            throw new InvalidDataException($"Monster {monster} has no grade.");
        }

        for (var i = 0; i < grades.Count; i++)
        {
            if (grades[i].Grade != i + 1)
            {
                throw new InvalidDataException($"Monster {monster}'s grade {i + 1} is numbered {grades[i].Grade}.");
            }

            if (grades[i].MonsterId != monster)
            {
                throw new InvalidDataException($"Monster {monster}'s grade {i + 1} is of monster {grades[i].MonsterId}.");
            }
        }

        return [.. grades.Select(grade => grade.Level)];
    }

    /// <summary>Reads a drop whose criterion is empty for none, as its <c>hasCriterions</c> says.</summary>
    /// <exception cref="InvalidDataException">The drop is of another monster, a chance is not a percentage, or its criterion contradicts its flag.</exception>
    public static MonsterDrop DropOf(int monster, int dropMonster, int item, IReadOnlyList<float> chances, string criterion, bool hasCriterion)
    {
        if (dropMonster != monster)
        {
            throw new InvalidDataException($"Monster {monster} lists a drop of monster {dropMonster}.");
        }

        for (var i = 0; i < chances.Count; i++)
        {
            if (chances[i] is not (>= 0 and <= 100))
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Monster {monster} drops item {item} at {chances[i]} % for grade {i + 1}."));
            }
        }

        if (criterion.Length > 0 != hasCriterion)
        {
            throw new InvalidDataException($"Monster {monster}'s drop of item {item} has a criterion '{criterion}' where its flag says {hasCriterion}.");
        }

        return new MonsterDrop { ItemId = item, Chances = chances, Criterion = criterion.Length > 0 ? criterion : null };
    }

    /// <exception cref="InvalidDataException">A race names text 0.</exception>
    public static IReadOnlyList<MonsterRace> ReadRaces(IEnumerable<Fields> records, Func<long, Text?> name)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new MonsterRace { Id = id, Name = ItemTables.Named("Monster race", id, name(record.Int32("nameId"))), SuperRaceId = record.Int32("superRaceId") };
                })
                .OrderBy(race => race.Id),
        ];
    }

    /// <exception cref="InvalidDataException">A super race names text 0.</exception>
    public static IReadOnlyList<MonsterSuperRace> ReadSuperRaces(IEnumerable<Fields> records, Func<long, Text?> name)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new MonsterSuperRace { Id = id, Name = ItemTables.Named("Monster super race", id, name(record.Int32("nameId"))) };
                })
                .OrderBy(superRace => superRace.Id),
        ];
    }

    /// <summary>Leaves out the races of a super race the client lacks and their monsters, counted, and checks the other references the app relies on.</summary>
    /// <remarks>Race 347 names super race 50, which 3.7.13.17 lacks, and holds 6 monsters no sub-area lists: left out by the owner's choice of 2026-11-17.</remarks>
    /// <exception cref="InvalidDataException">An id is held twice, or a monster's race, a drop's item or a sub-area's monster is missing.</exception>
    public static (IReadOnlyList<Monster> Monsters, IReadOnlyList<MonsterRace> Races, int SkippedRaces, int SkippedMonsters) Kept(IReadOnlyList<Monster> monsters, IReadOnlyList<MonsterRace> races, IReadOnlyList<MonsterSuperRace> superRaces, IReadOnlyCollection<int> items, IEnumerable<int[]> subAreaMonsters)
    {
        ItemTables.Unique("monsters", monsters, monster => monster.Id);

        var raceIds = ItemTables.Unique("monster races", races, race => race.Id);
        var superRaceIds = ItemTables.Unique("monster super races", superRaces, superRace => superRace.Id);
        var itemIds = items.ToHashSet();

        if (monsters.FirstOrDefault(monster => !raceIds.Contains(monster.RaceId)) is { } orphan)
        {
            throw new InvalidDataException($"Monster {orphan.Id} is of race {orphan.RaceId}, which the client lacks.");
        }

        IReadOnlyList<MonsterRace> keptRaces = [.. races.Where(race => superRaceIds.Contains(race.SuperRaceId))];
        var keptRaceIds = keptRaces.Select(race => race.Id).ToHashSet();
        IReadOnlyList<Monster> kept = [.. monsters.Where(monster => keptRaceIds.Contains(monster.RaceId))];
        var known = kept.Select(monster => monster.Id).ToHashSet();

        foreach (var monster in kept)
        {
            if (monster.Drops.FirstOrDefault(drop => !itemIds.Contains(drop.ItemId)) is { } drop)
            {
                throw new InvalidDataException($"Monster {monster.Id} drops item {drop.ItemId}, which items.json lacks.");
            }
        }

        if (subAreaMonsters.SelectMany(ids => ids).FirstOrDefault(id => !known.Contains(id), -1) is not -1 and var lost)
        {
            throw new InvalidDataException($"A sub-area names monster {lost}, which monsters.json lacks.");
        }

        return (kept, keptRaces, races.Count - keptRaces.Count, monsters.Count - kept.Count);
    }

    public static byte[] WriteMonsters(IEnumerable<Monster> monsters)
    {
        return JsonTable.Write(monsters, (json, monster) =>
        {
            json.WriteNumber("id", monster.Id);
            JsonTable.WriteText(json, "name", monster.Name);
            json.WriteNumber("raceId", monster.RaceId);
            json.WriteStartArray("flags");

            foreach (var flag in monster.Flags)
            {
                json.WriteStringValue(flag);
            }

            json.WriteEndArray();
            json.WriteStartArray("levels");

            foreach (var level in monster.Levels)
            {
                json.WriteNumberValue(level);
            }

            json.WriteEndArray();
            json.WriteStartArray("drops");

            foreach (var drop in monster.Drops)
            {
                json.WriteStartObject();
                json.WriteNumber("itemId", drop.ItemId);
                json.WriteStartArray("chances");

                foreach (var chance in drop.Chances)
                {
                    json.WriteNumberValue(chance);
                }

                json.WriteEndArray();
                json.WriteString("criterion", drop.Criterion);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        });
    }

    public static byte[] WriteRaces(IEnumerable<MonsterRace> races)
    {
        return JsonTable.Write(races, (json, race) =>
        {
            json.WriteNumber("id", race.Id);
            JsonTable.WriteText(json, "name", race.Name);
            json.WriteNumber("superRaceId", race.SuperRaceId);
        });
    }

    public static byte[] WriteSuperRaces(IEnumerable<MonsterSuperRace> superRaces)
    {
        return JsonTable.Write(superRaces, (json, superRace) =>
        {
            json.WriteNumber("id", superRace.Id);
            JsonTable.WriteText(json, "name", superRace.Name);
        });
    }

    /// <exception cref="InvalidDataException">The drop is not what the extractor measured, or the client gives chances for a sixth grade.</exception>
    private static MonsterDrop ReadDrop(int monster, Fields drop)
    {
        // A chance for a sixth grade would be a field the folder silently leaves out.
        if (drop.Has($"percentDropForGrade{DropGrades + 1}"))
        {
            throw new InvalidDataException($"Monster {monster}'s drops give chances for more than {DropGrades} grades.");
        }

        float[] chances = [.. Enumerable.Range(1, DropGrades).Select(grade => drop.Single(string.Create(CultureInfo.InvariantCulture, $"percentDropForGrade{grade}")))];

        return DropOf(monster, drop.Int32("monsterId"), drop.Int32("objectId"), chances, drop.String("criterions"), drop.Boolean("hasCriterions"));
    }
}
