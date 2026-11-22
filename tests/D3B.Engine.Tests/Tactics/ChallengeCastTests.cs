using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A challenge of the blows and the kills is kept or broken cast by cast (D497).</summary>
public sealed class ChallengeCastTests
{
    private const long Target = -1;
    private const long Other = -2;
    private const long Third = -3;

    private static readonly EffectElement[] _earth = [EffectElement.Earth];

    private static ImaginedFight Fight(params Combatant[] others)
    {
        var actor = Caster();

        return new ImaginedFight { Map = Open(), Fighters = [actor, .. others], ActorId = actor.Id, Round = 2 };
    }

    private static ImaginedFight Fight()
    {
        return Fight(Enemy(Target, Off(2, 0)) with { Level = 10 }, Enemy(Other, Off(0, 2)) with { Level = 20 }, Enemy(Third, Off(-2, 0)) with { Level = 30 });
    }

    private static ImaginedFight Struck(ImaginedFight fight, long id, int loss = 10)
    {
        var fighter = fight.Find(id)!;

        return fight.With(fighter with { Life = Math.Max(0, fighter.Life - loss), IsAlive = fighter.Life > loss });
    }

    private static ImaginedFight Killed(ImaginedFight fight, long id)
    {
        return Struck(fight, id, fight.Find(id)!.Life);
    }

    private static bool Keeps(TurnChallenge challenge, ImaginedFight before, ImaginedFight after)
    {
        return challenge.Cast(before, after, _earth) is not null;
    }

    [Fact]
    public void PremierKillsNoEnemyBeforeItsTarget()
    {
        var premier = TurnChallenge.Of("CK~1", 100)! with { TargetId = Target };
        var fight = Fight();

        Assert.False(Keeps(premier, fight, Killed(fight, Other)));
        Assert.True(Keeps(premier, fight, Struck(fight, Other)));
        Assert.True(Keeps(premier, fight, Killed(fight, Target)));
        Assert.True(Keeps(premier, Killed(fight, Target), Killed(Killed(fight, Target), Other)));

        // One cast killing the target and another enemy at once: the server may kill the target first.
        Assert.True(Keeps(premier, fight, Killed(Killed(fight, Target), Other)));
    }

    [Fact]
    public void DernierKillsItsTargetOnlyOnceTheOthersAreDead()
    {
        var dernier = TurnChallenge.Of("Ck~1", 100)! with { TargetId = Target };
        var fight = Fight();
        var last = Killed(Killed(fight, Other), Third);

        Assert.False(Keeps(dernier, fight, Killed(fight, Target)));
        Assert.True(Keeps(dernier, fight, Killed(fight, Other)));
        Assert.True(Keeps(dernier, last, Killed(last, Target)));
        Assert.True(Keeps(dernier, Killed(fight, Other), Killed(Killed(Killed(fight, Other), Third), Target)));
    }

    [Fact]
    public void CruelKillsTheEnemiesFromTheLowestLevel()
    {
        var cruel = TurnChallenge.Of("SL<1", 100)!;
        var fight = Fight();
        var tie = fight.With(fight.Find(Other)! with { Level = 10 });

        Assert.True(Keeps(cruel, fight, Killed(fight, Target)));
        Assert.False(Keeps(cruel, fight, Killed(fight, Other)));
        Assert.True(Keeps(cruel, Killed(fight, Target), Killed(Killed(fight, Target), Other)));
        Assert.True(Keeps(cruel, fight, Killed(Killed(fight, Target), Other)));
        Assert.False(Keeps(cruel, fight, Killed(Killed(fight, Target), Third)));
        Assert.True(Keeps(cruel, tie, Killed(tie, Other)));
    }

    [Fact]
    public void OrdonneKillsTheEnemiesFromTheHighestLevel()
    {
        var ordered = TurnChallenge.Of("SL>1", 100)!;
        var fight = Fight();

        Assert.True(Keeps(ordered, fight, Killed(fight, Third)));
        Assert.False(Keeps(ordered, fight, Killed(fight, Target)));
        Assert.True(Keeps(ordered, Killed(fight, Third), Killed(Killed(fight, Third), Other)));
    }

    [Fact]
    public void ElitisteStrikesNoEnemyButItsTargetWhileItLives()
    {
        var elitist = TurnChallenge.Of("CK~!,1", 100)! with { TargetId = Target };
        var fight = Fight();
        var dead = Killed(fight, Target);

        Assert.False(Keeps(elitist, fight, Struck(fight, Other)));
        Assert.False(Keeps(elitist, fight, Struck(Struck(fight, Target), Other)));
        Assert.True(Keeps(elitist, fight, Struck(fight, Target)));
        Assert.True(Keeps(elitist, dead, Struck(dead, Other)));
    }

    [Fact]
    public void TueurAVueStrikesItsTargetThenTheNearestEnemyOnceItIsDead()
    {
        var sight = TurnChallenge.Of("CO=e0,m1,tk,n", 100)! with { TargetId = Target };
        var fight = Fight(Enemy(Target, Off(1, 0)), Enemy(Other, Off(0, 4)), Enemy(Third, Off(-2, 0)));
        var next = sight.Cast(fight, Killed(fight, Target), _earth);

        Assert.False(Keeps(sight, fight, Struck(fight, Third)));
        Assert.Equal(Third, next?.TargetId);
        Assert.Null(next!.Cast(Killed(fight, Target), Struck(Killed(fight, Target), Other), _earth));
    }

    [Fact]
    public void TueurAGagesKillsOnlyItsTargetAndNoneOnceItIsDeadUntilTheServerNamesTheNext()
    {
        var hitman = TurnChallenge.Of("CC=e0,m1,k,k", 100)! with { TargetId = Target };
        var fight = Fight();
        var dead = Killed(fight, Target);

        Assert.True(Keeps(hitman, fight, Killed(fight, Target)));
        Assert.False(Keeps(hitman, fight, Killed(fight, Other)));
        Assert.True(Keeps(hitman, fight, Struck(fight, Other)));
        Assert.False(Keeps(hitman, dead, Killed(dead, Other)));
        Assert.True(Keeps(hitman with { TargetId = Other }, dead, Killed(dead, Other)));
    }

    [Fact]
    public void FocusStrikesNoOtherEnemyUntilTheOneStruckIsDead()
    {
        var focus = TurnChallenge.Of("SF=1", 100)!;
        var fight = Fight();
        var struck = focus.Cast(fight, Struck(fight, Target), _earth);
        var dead = Killed(fight, Target);

        Assert.Equal(Target, struck?.FocusId);
        Assert.Null(struck!.Cast(fight, Struck(fight, Other), _earth));
        Assert.Null(focus.Cast(fight, Struck(Struck(fight, Target), Other), _earth));
        Assert.Null(struck.Cast(fight, dead, _earth)?.FocusId);
        Assert.Equal(Other, (focus with { FocusId = Target }).Cast(dead, Struck(dead, Other), _earth)?.FocusId);
    }

    [Fact]
    public void CruelAndOrdonneLeaveTheEnemiesSummonsOut()
    {
        var cruel = TurnChallenge.Of("SL<1", 100)!;
        var fight = Fight();
        var summoned = fight with { Fighters = [.. fight.Fighters, Enemy(-4, Off(3, 3)) with { Level = 1, SummonerId = Third }] };

        Assert.True(Keeps(cruel, summoned, Killed(summoned, Target)));
        Assert.True(Keeps(cruel, summoned, Killed(summoned, -4)));
    }

    [Fact]
    public void ElementaireStrikesInTheElementTheFightersFirstBlowBoundItTo()
    {
        var elemental = TurnChallenge.Of("SC=hc0,e,s", 100)!;
        var fight = Fight();
        var struck = Struck(fight, Target);
        EffectElement[] fire = [EffectElement.Fire];
        EffectElement[] both = [EffectElement.Earth, EffectElement.Fire];

        Assert.Equal(EffectElement.Earth, elemental.Cast(fight, struck, _earth)?.Element);
        Assert.Null(elemental.Cast(fight, struck, both));
        Assert.Null((elemental with { Element = EffectElement.Earth }).Cast(fight, struck, fire));
        Assert.NotNull((elemental with { Element = EffectElement.Earth }).Cast(fight, fight, fire));
        Assert.NotNull((elemental with { Element = EffectElement.Neutral }).Cast(fight, struck, [EffectElement.Neutral]));
    }

    [Fact]
    public void SansCoeurHealsNoAllyButTheFighterItself()
    {
        var heartless = TurnChallenge.Of("SO!e0,hc0,h", 100)!;
        var fight = Fight(new Combatant { Id = 2, Team = 0, Cell = Off(1, 0), Life = 50, MaxLife = 100 });
        var healed = fight.With(fight.Find(2)! with { Life = 70 });
        var self = fight.With(fight.Actor with { Life = 60 }) with { };
        var wounded = fight.With(fight.Actor with { Life = 50 });

        Assert.Null(heartless.Cast(fight, healed, _earth));
        Assert.NotNull(heartless.Cast(wounded, self, _earth));
    }
}
