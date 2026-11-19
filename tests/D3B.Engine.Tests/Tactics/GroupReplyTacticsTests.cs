using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>An enemy of the reply chooses between the characters of a group as real monsters do, the character playing no dearer to it than its allies.</summary>
public sealed class GroupReplyTacticsTests
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
    public void AnEnemyBetweenTheCharacterAndAnAllyOfItsGroupStrikesTheOneItHurtsMostWithoutHoldingTheCharacterDearer(string weights)
    {
        // Measured on the owner's group captures (the study by breed, T2): with the character's life counted twice in its
        // choice, the reply foresaw 10,574 on the character for 2,360 dealt. A blow of 20 takes 50 to 30 from the character, 40
        // to 20 from its ally: the ally's loss is the larger, the character's the larger only when counted twice.
        var actor = Caster() with { Life = 50 };
        var ally = Caster(Off(4, 0)) with { Id = 2, BreedId = 8, Life = 40 };
        var enemy = Enemy(-1, Off(2, 0)) with { ActionPoints = 3, MovementPoints = 0 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, ally, enemy], ActorId = actor.Id, Round = 1 };
        var knowledge = new FightKnowledge
        {
            Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, []).Add(ally.Id, []).Add(enemy.Id, [Level(_tactics.Blows.Spells, Blow)]),
        };

        var replied = _tactics.Replies(fight, knowledge, _tactics.JudgeOf(fight, knowledge, Named(weights)));

        Assert.Equal((50, 20), (replied.Find(actor.Id)!.Life, replied.Find(ally.Id)!.Life));
    }
}
