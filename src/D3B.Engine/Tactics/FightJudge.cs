using D3B.Data.Spells;
using D3B.Engine.Criteria;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Tactics;

/// <summary>How good an imagined fight is for the fighter playing's side, once its turn ends: one number, the higher the better.</summary>
/// <remarks>
/// <para>
/// Every term is in life, through the effects and never per class (D192, D201):
/// </para>
/// <list type="bullet">
/// <item><description>An ally's life, shields counted, worth less as it grows: a point healed on a fighter at a quarter of its
/// life is worth twice one at its most, so that the AI heals who needs it; its being alive, half its most life; the fighter
/// playing's counted <see cref="TacticWeights.Survival"/> times.</description></item>
/// <item><description>An enemy's life, each point weighed by how hard it hits, so that the AI turns on the dangerous ones; its
/// being alive, one and a half of its best turn's damage, so that the AI finishes a fighter off.</description></item>
/// <item><description>The threat: what each living enemy may take next from the ally it hits hardest, if it reaches it with its
/// MP and its spells' range, lowering that ally's worth; a blow that would kill counts its death too.</description></item>
/// <item><description>The approach: what the fighter playing may take off an enemy next turn from where it stands, its life
/// and, were it killed, its being alive, never more than killing it now would; and a little of it for each cell it comes
/// nearer an enemy it cannot reach yet.</description></item>
/// </list>
/// <para>
/// A fighter's best turn's damage on another comes from its spells as the knowledge lists them, each effect that damages at
/// the middle of its range through the formulas (<see cref="EffectFormulas"/>), the AP shared among them at their limits a
/// turn; reckoned once, from where the fight starts, for every number of AP. Line of sight and the cells between are left
/// to the search, which casts.
/// </para>
/// </remarks>
public sealed class FightJudge
{
    private const int MostPoints = 24;
    private const int MostCasts = 6;
    private const double AliveShare = 0.5;
    private const double KillTurns = 1.5;

    private readonly Combatant[] _fighters;
    private readonly long _actorId;
    private readonly int _team;
    private readonly TacticWeights _weights;
    private readonly double[][][] _damage;
    private readonly int[] _range;
    private readonly bool[] _mobile;
    private readonly double[] _hardest;
    private readonly int _actorTurnPoints;
    private readonly int _actorTurnMovement;
    private readonly double _hardestOfAll;

    /// <summary>Initializes a new instance of the <see cref="FightJudge"/> class, from the fight as it stands when the search starts.</summary>
    /// <param name="start">The fight.</param>
    /// <param name="knowledge">What the AI knows of the fighters' spells and points.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="spells">The pack's spells.</param>
    /// <param name="actions">What each effect does.</param>
    /// <param name="rules">The rules of a cast, for the effects' masks.</param>
    public FightJudge(ImaginedFight start, FightKnowledge knowledge, TacticWeights weights, SpellCatalog spells, EffectActions actions, CastRules rules)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(rules);

        _fighters = [.. start.Fighters];
        _actorId = start.ActorId;
        _team = start.Actor.Team;
        _weights = weights;
        _damage = new double[_fighters.Length][][];
        _range = new int[_fighters.Length];
        _mobile = new bool[_fighters.Length];
        _hardest = new double[_fighters.Length];

        var turn = knowledge.TurnPoints.TryGetValue(_actorId, out var points) ? points : (start.Actor.ActionPoints, start.Actor.MovementPoints);

        _actorTurnPoints = Math.Clamp(turn.Item1, 0, MostPoints);
        _actorTurnMovement = Math.Max(0, turn.Item2);

        for (var one = 0; one < _fighters.Length; one++)
        {
            var fighter = _fighters[one];
            var levels = knowledge.SpellsOf(fighter.Id);

            _damage[one] = new double[_fighters.Length][];
            _mobile[one] = !fighter.IsStatic && (fighter.MonsterId is null || (fighter.MonsterProperties & Data.Monsters.MonsterProperties.CanMove) is not 0);
            _range[one] = Range(spells, actions, levels, fighter);

            for (var other = 0; other < _fighters.Length; other++)
            {
                _damage[one][other] = fighter.Team == _fighters[other].Team ? [] : BestTurns(spells, actions, rules, levels, fighter, _fighters[other]);

                if (_damage[one][other].Length > 0)
                    _hardest[one] = Math.Max(_hardest[one], _damage[one][other][^1]);
            }
        }

        _hardestOfAll = _hardest.DefaultIfEmpty(0).Max();
    }

    private static double LifeWorth(double life, int most)
    {
        if (most <= 0)
            return Math.Max(0, life);

        // Concave: a point is worth more the lower the life, so that heals go to who needs them.
        return most * Math.Sqrt(Math.Clamp(life / most, 0, 2));
    }

    private static double Effective(Combatant fighter)
    {
        return fighter.Life + Math.Max(0, fighter.Characteristic(FightCharacteristic.Shield));
    }

    private static double Reaching(int distance, int reach)
    {
        // Within reach it lands; a cell or two beyond, the fighter may come closer meanwhile.
        return distance <= reach ? 1 : distance == reach + 1 ? 0.5 : distance == reach + 2 ? 0.25 : 0;
    }

    private static double Nearing(int distance, int reach)
    {
        // Beyond its reach, each cell nearer still counts a little, so that the fighter playing comes to the enemies it cannot
        // strike yet instead of waiting where it stands.
        return distance <= reach ? 1 : 0.5 * Math.Pow(0.85, distance - reach - 1);
    }

    private static int Range(SpellCatalog spells, EffectActions actions, IReadOnlyList<SpellLevel> levels, Combatant fighter)
    {
        var range = 0;

        foreach (var level in levels)
        {
            if (!Damages(spells, actions, level))
                continue;

            var most = level.Range + ((level.Properties & SpellLevelProperties.RangeCanBeBoosted) is not 0 ? Math.Max(0, fighter.RangeBonus) : 0);

            range = Math.Max(range, most);
        }

        return range;
    }

    private static bool Damages(SpellCatalog spells, EffectActions actions, in SpellLevel level)
    {
        foreach (var effect in spells.EffectsOf(level))
        {
            if (actions.Of(effect.EffectId).Kind is EffectKind.Damage or EffectKind.Steal or EffectKind.FixedDamage)
                return true;
        }

        return false;
    }

    private static double[] BestTurns(SpellCatalog spells, EffectActions actions, CastRules rules, IReadOnlyList<SpellLevel> levels, Combatant caster, Combatant target)
    {
        // The best damage for each number of AP, each spell cast at most as its limits a turn and a target allow.
        var best = new double[MostPoints + 1];
        Combatant[] pair = [caster, target];

        foreach (var level in levels)
        {
            var damage = Damage(spells, actions, rules, level, caster, target, pair);
            var cost = Math.Max(1, (int)level.ApCost);

            if (damage <= 0)
                continue;

            var casts = MostCasts;

            if (level.MaxCastPerTurn > 0)
                casts = Math.Min(casts, level.MaxCastPerTurn);

            if (level.MaxCastPerTarget > 0)
                casts = Math.Min(casts, level.MaxCastPerTarget);

            for (var cast = 0; cast < casts; cast++)
            {
                for (var points = MostPoints; points >= cost; points--)
                    best[points] = Math.Max(best[points], best[points - cost] + damage);
            }
        }

        return best;
    }

    private static double Damage(SpellCatalog spells, EffectActions actions, CastRules rules, in SpellLevel level, Combatant caster, Combatant target, Combatant[] pair)
    {
        var total = 0.0;

        foreach (var effect in spells.EffectsOf(level))
        {
            var action = actions.Of(effect.EffectId);

            if (action.Kind is not (EffectKind.Damage or EffectKind.Steal or EffectKind.FixedDamage) || (effect.Properties & SpellEffectProperties.ForClientOnly) is not 0)
                continue;

            var selected = rules.MaskOf(effect).Selects(caster, target, pair);

            if (selected is CriterionAnswer.False)
                continue;

            var roll = EffectFormulas.Roll(effect);
            var element = EffectFormulas.Resolve(action.Element, caster);
            var value = action.Kind is EffectKind.FixedDamage
                ? roll
                : EffectFormulas.Received(EffectFormulas.Dealt(roll, caster, element, critical: false), caster, target, element, melee: level.Range <= 1);
            var middle = (value.Min + value.Max) / 2.0;

            total += selected is CriterionAnswer.True ? middle : middle / 2;
        }

        return total;
    }

    /// <summary>Gets the damage a fighter may deal another in a turn with some AP, wherever both stand.</summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="target">The target.</param>
    /// <param name="actionPoints">The attacker's AP.</param>
    /// <returns>The damage, at the middle of the rolls; 0 for a fighter the judge did not start with.</returns>
    public double TurnDamage(long attacker, long target, int actionPoints)
    {
        var one = IndexOf(attacker);
        var other = IndexOf(target);

        if (one < 0 || other < 0 || _damage[one][other].Length is 0)
            return 0;

        return _damage[one][other][Math.Clamp(actionPoints, 0, MostPoints)];
    }

    /// <summary>Judges a fight once the fighter playing's turn ends.</summary>
    /// <param name="fight">The fight imagined.</param>
    /// <returns>Its worth for the fighter playing's side.</returns>
    public double Judge(ImaginedFight fight)
    {
        return Judge(fight, threatened: true);
    }

    /// <summary>Judges a fight once the enemies played the turns that follow the fighter playing's, as the search imagined them.</summary>
    /// <param name="fight">The fight once they played.</param>
    /// <returns>Its worth for the fighter playing's side, the threat left out: the enemies' blows are in the fighters' life.</returns>
    public double JudgeReplied(ImaginedFight fight)
    {
        return Judge(fight, threatened: false);
    }

    /// <summary>Gets what the fighter playing's side is worth alone: its fighters' life and their being alive, as <see cref="Judge(ImaginedFight)"/> counts them.</summary>
    /// <param name="fight">The fight.</param>
    /// <returns>The worth, which an enemy's reply lowers as much as it can.</returns>
    public double AlliesWorth(ImaginedFight fight)
    {
        ArgumentNullException.ThrowIfNull(fight);

        var worth = 0.0;

        foreach (var fighter in fight.Fighters)
        {
            if (fighter.IsAlive && fighter.Team == _team)
                worth += Weight(fighter) * (LifeWorth(Effective(fighter), fighter.MaxLife) + (AliveShare * fighter.MaxLife));
        }

        return worth;
    }

    private double Judge(ImaginedFight fight, bool threatened)
    {
        ArgumentNullException.ThrowIfNull(fight);

        var worth = 0.0;
        Span<double> feared = stackalloc double[fight.Fighters.Length];

        for (var index = 0; index < fight.Fighters.Length; index++)
        {
            var fighter = fight.Fighters[index];

            if (!fighter.IsAlive)
                continue;

            var known = IndexOf(fighter.Id);

            if (fighter.Team == _team)
            {
                worth += Weight(fighter) * (LifeWorth(Effective(fighter), fighter.MaxLife) + (AliveShare * fighter.MaxLife));

                continue;
            }

            worth -= Cost(fighter, known, Effective(fighter));

            if (threatened)
                Threaten(fight, fighter, known, feared);
        }

        for (var index = 0; index < fight.Fighters.Length; index++)
        {
            var ally = fight.Fighters[index];

            if (!ally.IsAlive || ally.Team != _team || feared[index] <= 0)
                continue;

            var effective = Effective(ally);
            var lost = Math.Min(feared[index], effective);
            var loss = LifeWorth(effective, ally.MaxLife) - LifeWorth(effective - lost, ally.MaxLife);

            if (feared[index] >= effective)
                loss += AliveShare * ally.MaxLife;

            worth -= _weights.Threat * Weight(ally) * loss;
        }

        return worth + (_weights.Approach * Approach(fight));
    }

    private double Cost(Combatant enemy, int known, double life)
    {
        // Its life, each point weighed by how hard it hits, and its being alive, which a kill takes away.
        var hardest = known < 0 ? 0 : _hardest[known];
        var share = _hardestOfAll > 0 ? hardest / _hardestOfAll : 1;

        return ((0.6 + (0.4 * share)) * life) + (KillTurns * hardest) + (0.1 * enemy.MaxLife);
    }

    private double Weight(Combatant ally)
    {
        return ally.Id == _actorId ? _weights.Survival : ally.IsSummon ? 0.3 : 1;
    }

    private void Threaten(ImaginedFight fight, Combatant enemy, int known, Span<double> feared)
    {
        if (known < 0 || enemy.Cell is Combatant.NoCell)
            return;

        var reach = _range[known] + (_mobile[known] ? Math.Max(0, enemy.MovementPoints) : 0);
        var best = 0.0;
        var hit = -1;

        // An enemy hits one fighter: the one it hurts most of those it reaches.
        for (var index = 0; index < fight.Fighters.Length; index++)
        {
            var ally = fight.Fighters[index];

            if (!ally.IsAlive || ally.Team != _team || ally.Cell is Combatant.NoCell)
                continue;

            var target = IndexOf(ally.Id);

            if (target < 0 || _damage[known][target].Length is 0)
                continue;

            var damage = _damage[known][target][Math.Clamp(enemy.ActionPoints, 0, MostPoints)] * Reaching(MapGeometry.Distance(enemy.Cell, ally.Cell), reach);

            if (damage > best)
            {
                best = damage;
                hit = index;
            }
        }

        if (hit >= 0)
            feared[hit] += best;
    }

    private double Approach(ImaginedFight fight)
    {
        var actor = fight.Find(_actorId);
        var known = IndexOf(_actorId);

        if (actor is not { IsAlive: true } || actor.Cell is Combatant.NoCell || known < 0)
            return 0;

        var reach = _range[known] + (_mobile[known] ? _actorTurnMovement : 0);
        var best = 0.0;

        foreach (var enemy in fight.Fighters)
        {
            if (!enemy.IsAlive || enemy.Team == _team || enemy.Cell is Combatant.NoCell)
                continue;

            var target = IndexOf(enemy.Id);

            if (target < 0 || _damage[known][target].Length is 0)
                continue;

            // Measured live (D203): next turn's damage unbounded outweighed a kill now, so that the AI spared a fighter at 14
            // life it could kill. It is worth what it would take off the enemy's cost, the kill's included, and never more.
            var life = Effective(enemy);
            var damage = Math.Min(_damage[known][target][_actorTurnPoints], life);
            var taken = Cost(enemy, target, life) - (damage >= life ? 0 : Cost(enemy, target, life - damage));

            best = Math.Max(best, taken * Nearing(MapGeometry.Distance(actor.Cell, enemy.Cell), reach));
        }

        return best;
    }

    private int IndexOf(long id)
    {
        for (var index = 0; index < _fighters.Length; index++)
        {
            if (_fighters[index].Id == id)
                return index;
        }

        return -1;
    }

    /// <summary>Gets how the fight looks for the fighter playing's side: the turns each side takes to beat the other.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="knowledge">The points each fighter starts its turn with.</param>
    /// <returns>The odds.</returns>
    public FightOdds Odds(ImaginedFight fight, FightKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);

        double ours = 0, theirs = 0, ourLife = 0, theirLife = 0;

        foreach (var fighter in fight.Fighters)
        {
            if (!fighter.IsAlive)
                continue;

            var known = IndexOf(fighter.Id);
            var points = knowledge.TurnPoints.TryGetValue(fighter.Id, out var turn) ? turn.ActionPoints : fighter.ActionPoints;
            var best = 0.0;

            for (var other = 0; known >= 0 && other < _fighters.Length; other++)
            {
                if (_damage[known][other].Length > 0 && fight.Find(_fighters[other].Id) is { IsAlive: true })
                    best = Math.Max(best, _damage[known][other][Math.Clamp(points, 0, MostPoints)]);
            }

            if (fighter.Team == _team)
            {
                ours += best;
                ourLife += Effective(fighter);
            }
            else
            {
                theirs += best;
                theirLife += Effective(fighter);
            }
        }

        return new FightOdds(ours > 0 ? theirLife / ours : double.PositiveInfinity, theirs > 0 ? ourLife / theirs : double.PositiveInfinity);
    }
}
