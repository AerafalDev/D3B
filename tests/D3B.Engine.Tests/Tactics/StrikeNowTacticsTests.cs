using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A blow the character can strike now is never left for its next turn, whatever its weights (D460).</summary>
public sealed class StrikeNowTacticsTests
{
    private const int Blow = 200;
    private const int EarthEffect = 97;

    private static readonly FightTactics _tactics = new(Catalog(), States(), new EffectCatalog(new Effect[]
    {
        new(EarthEffect, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    public static TheoryData<string> Weights => ["Default", "Group", "Prudent"];

    /// <summary>Spell 200, an earth blow of 20, 3 AP, range 1 to 4.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "a"];
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1200, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = EarthEffect, TargetMask = 0, Triggers = 1, DiceNum = 20, DiceSide = 20, Zone = Zone(SpellZoneShape.Point) },
        ];
        Spell[] spells = [new Spell { Id = Blow, NameId = Blow, FirstLevel = 0, LevelCount = 1 }];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static TacticWeights Named(string name)
    {
        return name switch
        {
            "Group" => TacticWeights.Group,
            "Prudent" => TacticWeights.Prudent,
            _ => TacticWeights.Default,
        };
    }

    [Theory]
    [MemberData(nameof(Weights))]
    public void TheCharacterKillsNowTheMonsterItCouldKillNextTurnToo(string weights)
    {
        // Measured on the owner's group run of 2026-11-19: beside allies, the AI passed 15 turns with a monster of 15 to 56 life
        // in reach, the kill next turn weighed 1.1 times the kill now.
        var actor = Caster() with { ActionPoints = 3, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(2, 0)) with { Life = 15, MaxLife = 15, ActionPoints = 3, MovementPoints = 0 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = actor.Id, Round = 1 };
        var knowledge = new FightKnowledge
        {
            Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, [Level(_tactics.Blows.Spells, Blow)]).Add(enemy.Id, []),
        };

        var plan = _tactics.Plan(fight, knowledge, Named(weights), TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });

        Assert.Equal([Blow], plan.Actions.Select(action => action.SpellId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.1)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public void AnApproachOfOneOrMoreNegativeOrNotANumberIsRefused(double approach)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TacticWeights { Approach = approach });
    }
}
