using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using D3B.Protocol.Game;

namespace D3B.Bot.Fighting;

/// <summary>Which group of a map the fighting activity attacks: those its settings allow, and of those the ones the AI judges the character beats (D198, D236).</summary>
/// <remarks>
/// Measured on the captures (9g): every monster of a map's groups is a grade of the pack's at that grade's level, so a group
/// is imagined as a fight from the pack alone, its monsters at their grades' life, points, characteristics and spells. The
/// character is the one its last fight's placement showed, at its life now; before any fight, a group is judged by levels,
/// none of its monsters above the character's.
/// </remarks>
public static class GroupJudge
{
    // The character imagined beside the group: the odds weigh no position, only who reaches whom in a turn.
    private const long Self = 1;

    // Measured on the owner's 54 groups the character was known for (D249): of the 13 the group would beat it in under a
    // quarter of the turns it needs, 11 were won; of the 20 under the turns it needs, 17. Only the first are turned away.
    private const double Margin = 0.25;

    /// <summary>Tells whether the settings allow attacking a group.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="group">The group.</param>
    /// <param name="level">The character's level.</param>
    /// <param name="monsters">The pack's monsters, which give a monster's family (D247).</param>
    /// <returns>Whether they allow it.</returns>
    public static bool Allows(FightSettings settings, MapMonsterGroup group, int level, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(monsters);

        var size = group.Monsters.Length;

        if (size < settings.LeastMonsters || size > settings.MostMonsters || group.Cell is null)
            return false;

        if (group.Monsters.Any(monster => settings.Excluded.Contains(monster.MonsterId)))
            return false;

        return settings.Mode is FightMode.Chosen
            ? group.Monsters.Any(monster => ChosenMonsters.Holds(settings, monsters, monster.MonsterId))
            : group.Monsters.All(monster => monster.Level <= level);
    }

    /// <summary>Tells whether the AI expects the character to beat a group: the group would not take its life in under a quarter of the turns it takes to beat the group, over the fight's course (D249).</summary>
    /// <param name="group">The group.</param>
    /// <param name="state">The character's state, out of a fight.</param>
    /// <param name="self">The character as its last fight's placement showed it, or <see langword="null"/> before any.</param>
    /// <param name="map">The map the group stands on.</param>
    /// <param name="tables">The pack's game tables.</param>
    /// <param name="tactics">The fight's AI.</param>
    /// <returns>Whether it does; before any fight, whether no monster of the group is above the character's level.</returns>
    public static bool Beats(MapMonsterGroup group, CharacterState state, Fighter? self, FightMap map, GameTables tables, FightTactics tactics)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(state);

        return OddsOf(group, state, self, map, tables, tactics) is { } odds
            ? odds.TurnsToLose >= odds.TurnsToWin * Margin
            : group.Monsters.All(monster => monster.Level <= (state.Level ?? 0));
    }

    /// <summary>Gets the odds the AI gives the character against a group, over the fight's course (D249).</summary>
    /// <param name="group">The group.</param>
    /// <param name="state">The character's state, out of a fight.</param>
    /// <param name="self">The character as its last fight's placement showed it, or <see langword="null"/> before any.</param>
    /// <param name="map">The map the group stands on.</param>
    /// <param name="tables">The pack's game tables.</param>
    /// <param name="tactics">The fight's AI.</param>
    /// <returns>The odds, or <see langword="null"/> when the group is judged by levels: before the character's first fight, or on a map too small to imagine it.</returns>
    public static FightOdds? OddsOf(MapMonsterGroup group, CharacterState state, Fighter? self, FightMap map, GameTables tables, FightTactics tactics)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(tactics);

        if (self is null || state.Level is not { } level)
            return null;

        var cells = Cells(map, group.Monsters.Length + 1);

        if (cells.Count <= group.Monsters.Length)
            return null;

        var life = state.Life is { } now && state.MaxLife is { } most ? (now, most) : (self.MaxLife, self.MaxLife);
        var character = FightCombatants.Of(self with { Id = Self, Team = FightTeam.Challenger, Cell = cells[0], IsAlive = true, Buffs = [], Life = life.Item1, MaxLife = life.Item2 }, tables.Monsters);
        var fighters = ImmutableArray.CreateBuilder<Combatant>();
        var spells = ImmutableDictionary.CreateBuilder<long, ImmutableArray<SpellLevel>>();
        var points = ImmutableDictionary.CreateBuilder<long, (int ActionPoints, int MovementPoints)>();

        fighters.Add(character);
        spells[Self] = CharacterSpells.Of(state.Spells, self.BreedId, level, tables.Spells, tables.Breeds);
        points[Self] = (character.ActionPoints, character.MovementPoints);

        for (var index = 0; index < group.Monsters.Length; index++)
        {
            var monster = group.Monsters[index];
            var id = -1L - index;

            if (!tables.Monsters.TryGetMonster(monster.MonsterId, out var found) || !tables.Monsters.TryGetGrade(monster.MonsterId, monster.Grade, out var grade))
                return new FightOdds(double.PositiveInfinity, 0);

            var combatant = Imagined(id, cells[index + 1], found, grade);

            fighters.Add(combatant);
            spells[id] = SpellsOf(tables, found, grade);
            points[id] = (combatant.ActionPoints, combatant.MovementPoints);
        }

        var fight = new ImaginedFight { Map = map, Fighters = fighters.ToImmutable(), ActorId = Self, Round = 1 };
        var knowledge = new FightKnowledge { Spells = spells.ToImmutable(), TurnPoints = points.ToImmutable() };

        // Over the fight's course, the group's members dying one after another, rather than all striking every turn (D249).
        return tactics.OddsOverTheFight(fight, knowledge);
    }

    private static List<int> Cells(FightMap map, int count)
    {
        var cells = new List<int>(count);

        for (var cell = 0; cell < MapData.CellCount && cells.Count < count; cell++)
        {
            if (map.IsWalkable(cell))
                cells.Add(cell);
        }

        return cells;
    }

    private static Combatant Imagined(long id, int cell, Monster monster, MonsterGrade grade)
    {
        var stats = grade.Characteristics;
        var life = stats.LifePoints + stats.Vitality;

        // The characteristics the effects' formulas read, from the grade (D195): elements' damage, bonuses and resistances,
        // which follow each other from earth's, earth, fire, water, air, then neutral.
        var characteristics = ImmutableSortedDictionary.CreateRange(new Dictionary<int, int>
        {
            [FightCharacteristic.Strength] = stats.Strength,
            [FightCharacteristic.Intelligence] = stats.Intelligence,
            [FightCharacteristic.Chance] = stats.Chance,
            [FightCharacteristic.Agility] = stats.Agility,
            [FightCharacteristic.AllDamageBonus] = stats.DamageBonus,
            [FightCharacteristic.EarthResistPercent] = stats.ReductionEarth,
            [FightCharacteristic.EarthResistPercent + 1] = stats.ReductionFire,
            [FightCharacteristic.EarthResistPercent + 2] = stats.ReductionWater,
            [FightCharacteristic.EarthResistPercent + 3] = stats.ReductionAir,
            [FightCharacteristic.EarthResistPercent + 4] = stats.ReductionNeutral,
            [FightCharacteristic.EarthReduction] = stats.ReductionEarthFlat,
            [FightCharacteristic.EarthReduction + 1] = stats.ReductionFireFlat,
            [FightCharacteristic.EarthReduction + 2] = stats.ReductionWaterFlat,
            [FightCharacteristic.EarthReduction + 3] = stats.ReductionAirFlat,
            [FightCharacteristic.EarthReduction + 4] = stats.ReductionNeutralFlat,
        });

        return new Combatant
        {
            Id = id,
            Team = (int)FightTeam.Defender,
            Cell = cell,
            Life = life,
            MaxLife = life,
            ActionPoints = stats.ActionPoints,
            MovementPoints = stats.MovementPoints,
            MonsterId = monster.Id,
            MonsterProperties = monster.Properties,
            Level = grade.Level,
            Characteristics = characteristics,
        };
    }

    private static ImmutableArray<SpellLevel> SpellsOf(GameTables tables, Monster monster, MonsterGrade grade)
    {
        var levels = ImmutableArray.CreateBuilder<SpellLevel>();

        foreach (var spell in tables.Monsters.SpellsOf(monster))
        {
            if (tables.Monsters.SpellGradeOf(spell, grade.Grade) is var spellGrade and > 0 && tables.Spells.TryGetLevel(spell.SpellId, spellGrade, out var level))
                levels.Add(level);
        }

        return levels.DrainToImmutable();
    }
}
