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
/// <item><description>The threat: what each living enemy may take next from the ally it hits hardest, with the spells that
/// land on it, lowering that ally's worth; a blow that would kill counts its death too.</description></item>
/// <item><description>The approach: what the fighter playing may take off an enemy next turn from where it stands, with the
/// spells that land on it, its life and, were it killed, its being alive, never more than killing it now would; and a
/// little of it for each cell it comes nearer an enemy none lands on yet.</description></item>
/// </list>
/// <para>
/// A fighter's best turn's damage on another comes from its spells as the knowledge lists them, each effect that damages at
/// the middle of its range through the formulas (<see cref="EffectFormulas"/>), a critical hit weighed by its chance
/// (D207), the AP shared among them at their limits a turn; reckoned once, from where the fight starts, for every number of
/// AP. A spell lands on a fighter when one of the cells its caster walks to with its MP has the fighter within the level's
/// range and, for a level that tests it, in its line of sight (D208).
/// </para>
/// <para>
/// The threat and the approach read the fighters as the search left them, their characteristics and states, so that a
/// boost, a deboost or a state the search cast counts on the turns that follow it: a fighter in a state that prevents it
/// casting or dealing damage deals none, one invulnerable takes none. A buff of the fighter playing's own lasts until its
/// turn of the last round it lasts begins (D193): it counts on the enemies' turns that come first, and on the fighter
/// playing's next turn when it lasts two rounds or more. Each round it lasts beyond, up to the rounds the fight is foreseen
/// to last still, it is worth the damage it adds to its side's turn, or takes off the other side's (D209).
/// </para>
/// </remarks>
public sealed class FightJudge
{
    private const int MostPoints = 24;
    private const int MostCasts = 6;
    private const double AliveShare = 0.5;
    private const double KillTurns = 1.5;

    // Which of a fighter's damaging spells land is told by the bits of a number.
    private const int Landings = 64;

    // The rounds a buff is weighed over at most, beyond the turns that follow it.
    private const int MostRounds = 4;

    // A point of damage the side playing deals more is worth about what an enemy's point of life costs (Cost).
    private const double DamageWorth = 0.8;

    private const int ActionPoints = 1;
    private const int MovementPoints = 23;

    private readonly Combatant[] _fighters;
    private readonly long _actorId;
    private readonly int _team;
    private readonly TacticWeights _weights;
    private readonly FightMoves _moves;
    private readonly SpellCatalog _spells;
    private readonly WeaponBlows _blows;
    private readonly EffectActions _actions;
    private readonly CastRules _rules;
    private readonly SpellStateCatalog _states;
    private readonly int[] _signatures;
    private readonly Dictionary<(int One, int Other, int Attacker, int Target), double[]> _changedDamage = [];
    private readonly Dictionary<(int One, int Other, int Attacker, int Target, ulong Landing), double[]> _changedTurns = [];
    private readonly SpellLevel[][] _levels;
    private readonly double[][][] _levelDamage;
    private readonly double[][][] _damage;
    private readonly Dictionary<(int One, int Other, ulong Landing), double[]> _landingTurns = [];
    private readonly Dictionary<(long Id, int Cell, int MovementPoints), int[]> _cells = [];
    private readonly Dictionary<(int One, long Target, int From, int To, int MovementPoints, int Actor), ulong> _landing = [];
    private readonly int[] _range;
    private readonly bool[] _mobile;
    private readonly double[] _hardest;
    private readonly int _actorTurnPoints;
    private readonly int _actorTurnMovement;
    private readonly double _hardestOfAll;
    private readonly int _rounds;

    /// <summary>Initializes a new instance of the <see cref="FightJudge"/> class, from the fight as it stands when the search starts.</summary>
    /// <param name="start">The fight.</param>
    /// <param name="knowledge">What the AI knows of the fighters' spells and points.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="blows">The pack's spells and the weapons' blows.</param>
    /// <param name="actions">What each effect does.</param>
    /// <param name="rules">The rules of a cast, for the effects' masks.</param>
    /// <param name="moves">How fighters walk, for the cells they cast from.</param>
    /// <param name="states">The pack's states, for what each does.</param>
    public FightJudge(ImaginedFight start, FightKnowledge knowledge, TacticWeights weights, WeaponBlows blows, EffectActions actions, CastRules rules, FightMoves moves, SpellStateCatalog states)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(blows);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(moves);
        ArgumentNullException.ThrowIfNull(states);

        _fighters = [.. start.Fighters];
        _actorId = start.ActorId;
        _team = start.Actor.Team;
        _weights = weights;
        _moves = moves;
        _spells = blows.Spells;
        _blows = blows;
        _actions = actions;
        _rules = rules;
        _states = states;
        _signatures = [.. _fighters.Select(Signature)];
        _levels = new SpellLevel[_fighters.Length][];
        _levelDamage = new double[_fighters.Length][][];
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
            var levels = knowledge.SpellsOf(fighter.Id).Where(level => Damages(_spells, actions, level)).Select(level => CastRules.Modified(level, fighter)).Take(Landings).ToArray();

            _levels[one] = levels;
            _levelDamage[one] = new double[_fighters.Length][];
            _damage[one] = new double[_fighters.Length][];
            _mobile[one] = !fighter.IsStatic && (fighter.MonsterId is null || (fighter.MonsterProperties & Data.Monsters.MonsterProperties.CanMove) is not 0);
            _range[one] = Range(levels, fighter);

            for (var other = 0; other < _fighters.Length; other++)
            {
                if (fighter.Team == _fighters[other].Team)
                {
                    _levelDamage[one][other] = [];
                    _damage[one][other] = [];

                    continue;
                }

                Combatant[] pair = [fighter, _fighters[other]];

                _levelDamage[one][other] = [.. levels.Select(level => Damage(level, fighter, _fighters[other], pair))];
                _damage[one][other] = BestTurns(levels, _levelDamage[one][other], All(levels.Length));
                _hardest[one] = Math.Max(_hardest[one], _damage[one][other][^1]);
            }
        }

        _hardestOfAll = _hardest.DefaultIfEmpty(0).Max();

        // A buff is weighed over the rounds the fight is foreseen to last still, as its odds reckon them.
        var odds = Odds(start, knowledge);

        _rounds = Math.Clamp((int)Math.Ceiling(Math.Min(odds.TurnsToWin, odds.TurnsToLose)), 1, MostRounds);
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

    private static double Nearing(int distance, int reach)
    {
        // Where no spell lands yet, each cell nearer still counts a little, so that the fighter playing comes to the enemies
        // it cannot strike yet instead of waiting where it stands.
        return 0.5 * Math.Pow(0.85, Math.Max(0, distance - reach - 1));
    }

    private static ulong All(int count)
    {
        return count >= Landings ? ulong.MaxValue : (1UL << count) - 1;
    }

    private static int Range(SpellLevel[] levels, Combatant fighter)
    {
        var range = 0;

        foreach (var level in levels)
            range = Math.Max(range, SpellRange.MaxRange(level, Math.Max(0, fighter.RangeBonus)));

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

    private static double[] BestTurns(SpellLevel[] levels, double[] damages, ulong landing)
    {
        // The best damage for each number of AP, each spell that lands cast at most as its limits a turn and a target allow.
        var best = new double[MostPoints + 1];

        for (var index = 0; index < levels.Length; index++)
        {
            var level = levels[index];
            var damage = damages[index];
            var cost = Math.Max(1, (int)level.ApCost);

            if (damage <= 0 || (landing & (1UL << index)) is 0)
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

    private static int Signature(Combatant fighter)
    {
        // What a fighter's damage dealt and received reads: its characteristics but the shield, which blows take, and its states.
        var hash = new HashCode();

        foreach (var (id, value) in fighter.Characteristics)
        {
            if (id is not FightCharacteristic.Shield)
                hash.Add((id, value));
        }

        foreach (var state in fighter.States)
            hash.Add(state);

        return hash.ToHashCode();
    }

    private static Combatant Without(Combatant fighter, ImaginedBuff buff)
    {
        if (buff.IsState)
            return fighter with { States = fighter.States.Remove(buff.Change) };

        return buff.Characteristic switch
        {
            ActionPoints => fighter with { ActionPoints = Math.Max(0, fighter.ActionPoints - buff.Change) },
            MovementPoints => fighter with { MovementPoints = Math.Max(0, fighter.MovementPoints - buff.Change) },
            _ => fighter with { Characteristics = fighter.Characteristics.SetItem(buff.Characteristic, EffectFormulas.ValueOf(fighter, buff.Characteristic) - buff.Change) },
        };
    }

    private double Damage(in SpellLevel level, Combatant caster, Combatant target, Combatant[] pair)
    {
        if (!Hurts(level, caster, target))
            return 0;

        // A critical hit weighed by its chance (D207), its level's critical effects if it has some.
        var chance = EffectFormulas.CriticalChance(level, caster);
        // A weapon's blow deals a weapon's damage, its critical hit adding the weapon's bonus (D220).
        int? weaponBonus = WeaponBlows.IsBlow(level) ? _blows.CriticalBonusOf(level) : null;
        var normal = Damage(_spells.EffectsOf(level), _actions, _rules, level, caster, target, pair, critical: false, weaponBonus);

        if (chance <= 0)
            return normal;

        var critical = Damage(level.CriticalEffectCount > 0 ? _spells.CriticalEffectsOf(level) : _spells.EffectsOf(level), _actions, _rules, level, caster, target, pair, critical: true, weaponBonus);

        return ((1 - chance) * normal) + (chance * critical);
    }

    private bool Hurts(in SpellLevel level, Combatant caster, Combatant target)
    {
        // What the states say of a blow: a caster that casts or deals nothing, a level its states forbid, such as a Zobal's
        // spell of another mask (D228), a target that takes nothing, or not at this range.
        if (!_rules.StatesAllow(level, caster))
            return false;

        foreach (var state in caster.States)
        {
            if (_states.TryGetState(state, out var found) && (found.Properties & SpellStateProperties.CantDealDamage) is not 0)
                return false;
        }

        foreach (var state in target.States)
        {
            if (!_states.TryGetState(state, out var found))
                continue;

            var melee = level.Range <= 1;

            if ((found.Properties & SpellStateProperties.Invulnerable) is not 0
                || (melee && (found.Properties & SpellStateProperties.InvulnerableMelee) is not 0)
                || (!melee && (found.Properties & SpellStateProperties.InvulnerableRange) is not 0))
            {
                return false;
            }
        }

        return true;
    }

    private double[] Turns(int one, Combatant attacker, int other, Combatant target, ulong landing)
    {
        // The fighters as the search left them: the tables of the start, unless a buff changed what their damage reads.
        var attackerSignature = ReferenceEquals(attacker.Characteristics, _fighters[one].Characteristics) && attacker.States == _fighters[one].States ? _signatures[one] : Signature(attacker);
        var targetSignature = ReferenceEquals(target.Characteristics, _fighters[other].Characteristics) && target.States == _fighters[other].States ? _signatures[other] : Signature(target);

        if (attackerSignature == _signatures[one] && targetSignature == _signatures[other])
            return LandingTurns(one, other, landing);

        var key = (one, other, attackerSignature, targetSignature);

        if (!_changedDamage.TryGetValue(key, out var damages))
        {
            Combatant[] pair = [attacker, target];

            _changedDamage[key] = damages = [.. _levels[one].Select(level => Damage(level, attacker, target, pair))];
        }

        if (!_changedTurns.TryGetValue((one, other, attackerSignature, targetSignature, landing), out var turns))
            _changedTurns[(one, other, attackerSignature, targetSignature, landing)] = turns = BestTurns(_levels[one], damages, landing);

        return turns;
    }

    private static double Damage(ReadOnlySpan<SpellEffect> effects, EffectActions actions, CastRules rules, in SpellLevel level, Combatant caster, Combatant target, Combatant[] pair, bool critical, int? weaponBonus)
    {
        var total = 0.0;

        foreach (var effect in effects)
        {
            var action = actions.Of(effect.EffectId);

            if (action.Kind is not (EffectKind.Damage or EffectKind.Steal or EffectKind.FixedDamage) || (effect.Properties & SpellEffectProperties.ForClientOnly) is not 0)
                continue;

            var selected = rules.MaskOf(effect).Selects(caster, target, pair);

            if (selected is CriterionAnswer.False)
                continue;

            var roll = EffectFormulas.Roll(effect);
            var element = EffectFormulas.Resolve(action.Element, caster);
            var dealt = weaponBonus is { } bonus ? EffectFormulas.DealtByWeapon(roll, caster, element, critical, bonus) : EffectFormulas.Dealt(roll, caster, element, critical);
            var value = action.Kind is EffectKind.FixedDamage
                ? roll
                : EffectFormulas.Received(dealt, caster, target, element, melee: level.Range <= 1, weapon: weaponBonus is not null);
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

    /// <summary>Judges a fight once the enemies replied, the fighter playing's next turn searched from there rather than reckoned.</summary>
    /// <param name="fight">The fight once they played.</param>
    /// <param name="nextTurn">What the fighter playing's next turn, as the search imagined it from there, adds to its side's worth (<see cref="Standing"/>).</param>
    /// <returns>Its worth for the fighter playing's side: the next turn weighed as the approach is, the approach itself where that turn finds nothing (D210).</returns>
    public double JudgeReplied(ImaginedFight fight, double nextTurn)
    {
        ArgumentNullException.ThrowIfNull(fight);

        var next = nextTurn > 0 ? nextTurn : Approach(fight);

        return Standing(fight) + (_weights.Approach * next) + (fight.Buffs.IsEmpty ? 0 : Lasting(fight));
    }

    /// <summary>Gets what a fight is worth as it stands for the fighter playing's side: its fighters' life and being alive, less what the enemies' cost.</summary>
    /// <param name="fight">The fight.</param>
    /// <returns>The worth, neither the threat nor the next turn counted.</returns>
    public double Standing(ImaginedFight fight)
    {
        ArgumentNullException.ThrowIfNull(fight);

        var worth = 0.0;

        foreach (var fighter in fight.Fighters)
        {
            if (!fighter.IsAlive)
                continue;

            worth += fighter.Team == _team
                ? Weight(fighter) * (LifeWorth(Effective(fighter), fighter.MaxLife) + (AliveShare * fighter.MaxLife))
                : -Cost(fighter, IndexOf(fighter.Id), Effective(fighter));
        }

        return worth;
    }

    /// <summary>Gets the fight as the fighter playing's next turn begins: its turn's AP and MP, the next round, and the buffs it gave for one round gone.</summary>
    /// <param name="fight">The fight once the enemies replied.</param>
    /// <returns>The fight its next turn starts from.</returns>
    public ImaginedFight NextTurnStart(ImaginedFight fight)
    {
        ArgumentNullException.ThrowIfNull(fight);

        var next = NextTurnOf(fight);
        var (points, movement) = NextTurnPoints(fight);

        if (next.Find(_actorId) is not { IsAlive: true } actor)
            return next;

        return next.With(actor with { ActionPoints = points, MovementPoints = movement }) with { ActorId = _actorId, Round = fight.Round + 1 };
    }

    /// <summary>Gets the life the threat reckons the enemies may take from a fighter of the side playing before it plays again.</summary>
    /// <param name="fight">The fight, as the fighter playing's turn ends.</param>
    /// <param name="allyId">The fighter.</param>
    /// <returns>The life, unweighed; 0 for a fighter of the other side or not in the fight.</returns>
    /// <remarks>What the bench sets against the life the fighter lost before its next turn (D206).</remarks>
    public double Threat(ImaginedFight fight, long allyId)
    {
        ArgumentNullException.ThrowIfNull(fight);

        Span<double> feared = stackalloc double[fight.Fighters.Length];

        foreach (var fighter in fight.Fighters)
        {
            if (fighter.IsAlive && fighter.Team != _team)
                Threaten(fight, fighter, IndexOf(fighter.Id), feared);
        }

        for (var index = 0; index < fight.Fighters.Length; index++)
        {
            if (fight.Fighters[index].Id == allyId)
                return feared[index];
        }

        return 0;
    }

    /// <summary>Gets the damage the approach reckons the fighter playing may deal next turn from where it stands, on the enemy it would hurt most.</summary>
    /// <param name="fight">The fight, as the fighter playing's turn ends.</param>
    /// <returns>The damage, unweighed, never more than the enemy's life.</returns>
    /// <remarks>What the bench sets against the damage the fighter dealt in its next turn (D206).</remarks>
    public double NextTurnDamage(ImaginedFight fight)
    {
        ArgumentNullException.ThrowIfNull(fight);

        var actor = fight.Find(_actorId);
        var known = IndexOf(_actorId);

        if (actor is not { IsAlive: true } || actor.Cell is Combatant.NoCell || known < 0)
            return 0;

        var best = 0.0;

        foreach (var enemy in fight.Fighters)
        {
            var target = IndexOf(enemy.Id);

            if (!enemy.IsAlive || enemy.Team == _team || enemy.Cell is Combatant.NoCell || target < 0 || _damage[known][target].Length is 0)
                continue;

            var (damage, share) = NextTurn(fight, actor, known, enemy, target);

            best = Math.Max(best, Math.Min(damage, Effective(enemy)) * share);
        }

        return best;
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

        return worth + (_weights.Approach * Approach(fight)) + (fight.Buffs.IsEmpty ? 0 : Lasting(fight));
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
        if (known < 0 || enemy.Cell is Combatant.NoCell || _levels[known].Length is 0)
            return;

        var best = 0.0;
        var hit = -1;

        // An enemy hits one fighter: the one it hurts most of those its spells land on from where it walks.
        for (var index = 0; index < fight.Fighters.Length; index++)
        {
            var ally = fight.Fighters[index];

            if (!ally.IsAlive || ally.Team != _team || ally.Cell is Combatant.NoCell)
                continue;

            var target = IndexOf(ally.Id);

            if (target < 0 || _damage[known][target].Length is 0)
                continue;

            var landing = Lands(fight, enemy, known, ally, Math.Max(0, enemy.MovementPoints));

            if (landing is 0)
                continue;

            var damage = Turns(known, enemy, target, ally, landing)[Math.Clamp(enemy.ActionPoints, 0, MostPoints)];

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
        fight = NextTurnOf(fight);

        var actor = fight.Find(_actorId);
        var known = IndexOf(_actorId);

        if (actor is not { IsAlive: true } || actor.Cell is Combatant.NoCell || known < 0)
            return 0;

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
            var (next, share) = NextTurn(fight, actor, known, enemy, target);
            var damage = Math.Min(next, life);
            var taken = Cost(enemy, target, life) - (damage >= life ? 0 : Cost(enemy, target, life - damage));

            best = Math.Max(best, taken * share);
        }

        return best;
    }

    private (double Damage, double Share) NextTurn(ImaginedFight fight, Combatant actor, int known, Combatant enemy, int target)
    {
        // Next turn the fighter playing walks with its turn's MP and casts with its turn's AP, those its lasting buffs add
        // among them, the spells that land then.
        var (points, movement) = NextTurnPoints(fight);
        var landing = Lands(fight, actor, known, enemy, _mobile[known] ? movement : 0);

        if (landing is not 0)
            return (Turns(known, actor, target, enemy, landing)[points], 1);

        var reach = _range[known] + (_mobile[known] ? movement : 0);

        return (Turns(known, actor, target, enemy, All(_levels[known].Length))[points], Nearing(MapGeometry.Distance(actor.Cell, enemy.Cell), reach));
    }

    private (int ActionPoints, int MovementPoints) NextTurnPoints(ImaginedFight fight)
    {
        var points = _actorTurnPoints;
        var movement = _actorTurnMovement;

        foreach (var buff in fight.Buffs)
        {
            if (buff.CasterId != _actorId || buff.FighterId != _actorId || buff.Duration is 1)
                continue;

            if (buff.Characteristic is ActionPoints)
                points += buff.Change;
            else if (buff.Characteristic is MovementPoints)
                movement += buff.Change;
        }

        return (Math.Clamp(points, 0, MostPoints), Math.Max(0, movement));
    }

    private ImaginedFight NextTurnOf(ImaginedFight fight)
    {
        // A buff the fighter playing gave for one round ends as its next turn begins: that turn does without it.
        var next = fight;

        foreach (var buff in fight.Buffs)
        {
            if (buff.CasterId == _actorId && buff.Duration is 1 && buff.Characteristic is not (ActionPoints or MovementPoints) && next.Find(buff.FighterId) is { } fighter)
                next = next.With(Without(fighter, buff));
        }

        return next;
    }

    private double Lasting(ImaginedFight fight)
    {
        // Each round a buff of the fighter playing's lasts beyond the turns that follow it, it is worth the damage it adds to
        // its side's turn and takes off the other side's, up to the rounds the fight is foreseen to last still.
        var worth = 0.0;

        foreach (var buff in fight.Buffs)
        {
            if (buff.CasterId != _actorId || fight.Find(buff.FighterId) is not { IsAlive: true } fighter)
                continue;

            var known = IndexOf(fighter.Id);
            var ours = buff.Duration < 0 ? _rounds - 1 : Math.Min(Math.Max(0, buff.Duration - 2), _rounds - 1);
            var theirs = buff.Duration < 0 ? _rounds - 1 : Math.Min(Math.Max(0, buff.Duration - 1), _rounds - 1);

            if (known < 0 || (ours <= 0 && theirs <= 0))
                continue;

            var without = Without(fighter, buff);
            var (dealt, taken) = fighter.Team == _team ? AllyBuff(fight, known, fighter, without) : EnemyBuff(fight, known, fighter, without);

            worth += (DamageWorth * _weights.Approach * dealt * ours) + (_weights.Threat * taken * theirs);
        }

        return worth;
    }

    private (double Dealt, double Taken) AllyBuff(ImaginedFight fight, int known, Combatant ally, Combatant without)
    {
        // What the ally deals more on the enemy it hurts most, and what the enemies deal it less, each a turn.
        var points = ally.Id == _actorId ? _actorTurnPoints : Math.Clamp(ally.ActionPoints, 0, MostPoints);
        double dealt = 0, taken = 0;

        foreach (var enemy in fight.Fighters)
        {
            var other = IndexOf(enemy.Id);

            if (!enemy.IsAlive || enemy.Team == _team || other < 0)
                continue;

            if (_levels[known].Length > 0)
                dealt = Math.Max(dealt, Turns(known, ally, other, enemy, All(_levels[known].Length))[points] - Turns(known, without, other, enemy, All(_levels[known].Length))[Math.Clamp(without.Id == _actorId ? points : without.ActionPoints, 0, MostPoints)]);

            if (_levels[other].Length > 0)
            {
                var enemyPoints = Math.Clamp(enemy.ActionPoints, 0, MostPoints);

                taken += Weight(ally) * (Turns(other, enemy, known, without, All(_levels[other].Length))[enemyPoints] - Turns(other, enemy, known, ally, All(_levels[other].Length))[enemyPoints]);
            }
        }

        return (dealt, taken);
    }

    private (double Dealt, double Taken) EnemyBuff(ImaginedFight fight, int known, Combatant enemy, Combatant without)
    {
        // What the fighter playing deals the enemy more, and what the enemy deals the ally it hurts most less, each a turn.
        var actor = fight.Find(_actorId);
        var self = IndexOf(_actorId);
        double dealt = 0, taken = 0;

        if (actor is { IsAlive: true } && self >= 0 && _levels[self].Length > 0)
            dealt = Turns(self, actor, known, enemy, All(_levels[self].Length))[_actorTurnPoints] - Turns(self, actor, known, without, All(_levels[self].Length))[_actorTurnPoints];

        if (_levels[known].Length is 0)
            return (dealt, 0);

        foreach (var ally in fight.Fighters)
        {
            var other = IndexOf(ally.Id);

            if (!ally.IsAlive || ally.Team != _team || other < 0)
                continue;

            var before = Turns(known, without, other, ally, All(_levels[known].Length))[Math.Clamp(without.ActionPoints, 0, MostPoints)];
            var after = Turns(known, enemy, other, ally, All(_levels[known].Length))[Math.Clamp(enemy.ActionPoints, 0, MostPoints)];

            taken = Math.Max(taken, Weight(ally) * (before - after));
        }

        return (dealt, taken);
    }

    private double[] LandingTurns(int one, int other, ulong landing)
    {
        if (landing == All(_levels[one].Length))
            return _damage[one][other];

        if (!_landingTurns.TryGetValue((one, other, landing), out var turns))
            _landingTurns[(one, other, landing)] = turns = BestTurns(_levels[one], _levelDamage[one][other], landing);

        return turns;
    }

    private ulong Lands(ImaginedFight fight, Combatant attacker, int one, Combatant target, int movementPoints)
    {
        // Which of the attacker's damaging spells land on the target from a cell it walks to: within range, and in sight for
        // a level that tests it. The attacker leaves its cell; the fighter playing's stands where the fight has it.
        var levels = _levels[one];
        var actorCell = fight.Find(_actorId)?.Cell ?? Combatant.NoCell;
        var key = (one, target.Id, attacker.Cell, target.Cell, movementPoints, actorCell);

        if (levels.Length is 0 || attacker.Cell is Combatant.NoCell || target.Cell is Combatant.NoCell)
            return 0;

        if (_landing.TryGetValue(key, out var landing))
            return landing;

        var cells = CellsOf(fight, attacker, one, movementPoints);
        var occupied = CastRules.OccupiedBy(fight.Fighters);

        occupied.Remove(attacker.Cell);

        for (var index = 0; index < levels.Length; index++)
        {
            var level = levels[index];
            var sight = (level.Properties & SpellLevelProperties.CastTestLos) is not 0;

            foreach (var cell in cells)
            {
                if (cell == target.Cell || !SpellRange.Reaches(level, attacker.RangeBonus, cell, target.Cell))
                    continue;

                if (sight && !LineOfSight.IsClear(fight.Map, occupied, cell, target.Cell))
                    continue;

                landing |= 1UL << index;

                break;
            }
        }

        _landing[key] = landing;

        return landing;
    }

    private int[] CellsOf(ImaginedFight fight, Combatant walker, int one, int movementPoints)
    {
        if (!_mobile[one] || movementPoints <= 0)
            return [walker.Cell];

        var key = (walker.Id, walker.Cell, movementPoints);

        if (_cells.TryGetValue(key, out var cells))
            return cells;

        // An enemy's walk is reckoned once wherever the fighter playing stands, which the search moves many times: without it.
        IReadOnlyList<Combatant> fighters = walker.Id == _actorId ? fight.Fighters : [.. fight.Fighters.Where(fighter => fighter.Id != _actorId)];

        _cells[key] = cells = [.. _moves.Reachable(fight.Map, fighters, walker with { MovementPoints = movementPoints }).Keys];

        return cells;
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
