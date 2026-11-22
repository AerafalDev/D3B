using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A state the AI sets on an enemy that the caster's own blows read, as a Huppermage's elemental states, and what it is worth (D494).</summary>
public sealed class EnemyStateTacticsTests
{
    private const int Mark = 10;
    private const int Strike = 20;
    private const int Shelter = 30;

    private const int Marked = 7;
    private const int Sheltered = 9;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(950, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 10, 3 AP at range 1 to 4: an earth blow of 20 that puts the state 7 on the enemy for the whole fight, as a
    /// Huppermage's spell of another element puts its elemental state. Spell 20, 3 AP at range 1 to 4: an earth blow of 40, and
    /// 12 more on an enemy in the state 7, as the combination the elemental states set off; none of it on an enemy in the
    /// state 9. Spell 30, 3 AP at range 1 to 4: the state 9 on the enemy for the whole fight, which spares it spell 20's blows.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "A,E7,e9", "A,e9"];
        var point = Zone(SpellZoneShape.Point);

        SpellLevel Level(int id, int spell, int first, int count)
        {
            return new SpellLevel
            {
                Id = id,
                SpellId = spell,
                Grade = 1,
                ApCost = 3,
                MinRange = 1,
                Range = 4,
                StatesCriterion = SpellLevel.NoCriterion,
                FirstEffect = first,
                EffectCount = (ushort)count,
            };
        }

        SpellEffect Effect(int effectId, int mask, int dice = 0, int side = 0, int value = 0, int duration = 0)
        {
            return new SpellEffect { EffectId = (ushort)effectId, TargetMask = mask, Triggers = 1, DiceNum = dice, DiceSide = side, Value = value, Duration = (sbyte)duration, Zone = point };
        }

        SpellLevel[] levels =
        [
            Level(1010, Mark, 0, 2),
            Level(1020, Strike, 2, 2),
            Level(1030, Shelter, 4, 1),
        ];
        SpellEffect[] effects =
        [
            Effect(97, 0, dice: 20, side: 20),
            Effect(950, 0, value: Marked, duration: -1),
            Effect(97, 3, dice: 40, side: 40),
            Effect(97, 2, dice: 12, side: 12),
            Effect(950, 0, value: Sheltered, duration: -1),
        ];
        Spell[] spells =
        [
            new Spell { Id = Mark, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Strike, NameId = 2, FirstLevel = 1, LevelCount = 1 },
            new Spell { Id = Shelter, NameId = 3, FirstLevel = 2, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(Combatant caster, Combatant enemy)
    {
        return new ImaginedFight { Map = Open(), Fighters = [caster, enemy], ActorId = caster.Id, Round = 1 };
    }

    private static FightKnowledge Knowledge(Combatant caster)
    {
        return new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(caster.Id, [Level(_spells, Mark), Level(_spells, Strike)]) };
    }

    [Fact]
    public void ACharacterStrikesHarderRatherThanMarkingAnEnemyForBlowsToCome()
    {
        // Reported by the owner (D494): a Huppermage of earth cycled its enemy's elemental states and struck air, each state
        // credited for the whole fight as the more its blows would deal, while the combination they set off takes them off.
        var caster = Caster() with { ActionPoints = 3, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(2, 0)) with { Life = 300, MaxLife = 300 };
        var plan = _tactics.Plan(Fight(caster, enemy), Knowledge(caster), TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Strike], plan.Actions.Select(action => action.SpellId));
    }

    [Fact]
    public void AStateSetOnAnEnemyIsWorthWhatItAddsToTheNextTurnAlone()
    {
        var caster = Caster() with { ActionPoints = 3, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(2, 0)) with { Life = 300, MaxLife = 300 };
        var fight = Fight(caster, enemy);
        var judge = _tactics.JudgeOf(fight, Knowledge(caster), TacticWeights.Default);

        var marked = _tactics.Imagination.Cast(fight, Level(_spells, Mark), enemy.Cell);

        Assert.Contains(Marked, marked.Find(enemy.Id)!.States);
        Assert.Equal(judge.Judge(marked with { Buffs = [] }), judge.Judge(marked), 6);
    }

    [Fact]
    public void AStateThatSparesAnEnemyTheSidesBlowsStillCostsItsRounds()
    {
        // The review's case (D494): a state keeping an enemy from taking the side's blows lowers them over every round it lasts.
        var caster = Caster() with { ActionPoints = 3, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(2, 0)) with { Life = 300, MaxLife = 300 };
        var fight = Fight(caster, enemy);
        var judge = _tactics.JudgeOf(fight, Knowledge(caster), TacticWeights.Default);

        var sheltered = _tactics.Imagination.Cast(fight, Level(_spells, Shelter), enemy.Cell);

        Assert.Contains(Sheltered, sheltered.Find(enemy.Id)!.States);
        Assert.True(judge.Judge(sheltered) < judge.Judge(sheltered with { Buffs = [] }) - 1);
    }
}
