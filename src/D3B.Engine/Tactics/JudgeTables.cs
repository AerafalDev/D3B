using System.Collections.Immutable;
using D3B.Data.Spells;
using D3B.Engine.Criteria;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using static D3B.Engine.Tactics.JudgeTurns;

namespace D3B.Engine.Tactics;

/// <summary>What the judge reckons once, from where the fight starts, and keeps: each fighter's damaging spells, the damage of each on every enemy, the best turn for every number of AP, and which spells land from where a fighter walks (D192, D208).</summary>
/// <remarks>
/// A fighter's best turn's damage on another comes from its spells as the knowledge lists them, each effect that damages at
/// the middle of its range through the formulas (<see cref="EffectFormulas"/>), a critical hit weighed by its chance (D207). The
/// tables are those of the fight as it starts: for fighters a buff changed, the damage is reckoned again and kept by what it reads
/// (<see cref="Turns"/>). A summon the search brings is one of the fighters the tables start with, by its monster, its grade and
/// its summoner, whatever its cell.
/// </remarks>
internal sealed class JudgeTables
{
    private readonly Combatant[] _fighters;
    private readonly Dictionary<(int MonsterId, int Grade, long SummonerId), int> _summons = [];
    private readonly long _actorId;
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
    private readonly double _hardestOfAll;

    /// <summary>Initializes a new instance of the <see cref="JudgeTables"/> class, from the fight as it stands when the search starts.</summary>
    /// <param name="start">The fight.</param>
    /// <param name="knowledge">What the AI knows of the fighters' spells.</param>
    /// <param name="blows">The pack's spells and the weapons' blows.</param>
    /// <param name="actions">What each effect does.</param>
    /// <param name="rules">The rules of a cast, for the effects' masks.</param>
    /// <param name="moves">How fighters walk, for the cells they cast from.</param>
    /// <param name="states">The pack's states, for what each does.</param>
    /// <param name="summons">The summons the fighter playing's spells may bring, each with the levels it casts.</param>
    public JudgeTables(ImaginedFight start, FightKnowledge knowledge, WeaponBlows blows, EffectActions actions, CastRules rules, FightMoves moves, SpellStateCatalog states, IReadOnlyList<(Combatant Summon, ImmutableArray<SpellLevel> Levels)> summons)
    {
        // A summon the search brings is judged as one of the fighters the judge starts with, by its monster, its grade and
        // its summoner, whatever its cell: its characteristics are its summoner's share (Summons), the same wherever it stands.
        _fighters = [.. start.Fighters, .. summons.Select(summon => summon.Summon)];
        _actorId = start.ActorId;
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

        for (var one = 0; one < _fighters.Length; one++)
        {
            var fighter = _fighters[one];
            var known = one < start.Fighters.Length ? knowledge.SpellsOf(fighter.Id) : summons[one - start.Fighters.Length].Levels;
            var levels = known.Where(level => Damages(_spells, actions, level)).Select(level => CastRules.Modified(level, fighter)).Take(Landings).ToArray();

            if (one >= start.Fighters.Length && fighter is { MonsterId: { } monster, Grade: { } grade, SummonerId: { } summoner })
                _summons.TryAdd((monster, grade, summoner), one);

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
    }

    /// <summary>Gets the fighters the tables start with, then the summons the search may bring.</summary>
    public IReadOnlyList<Combatant> Fighters => _fighters;

    /// <summary>Gets each fighter's damaging spells, at their levels as the fighter casts them.</summary>
    public SpellLevel[][] Levels => _levels;

    /// <summary>Gets, for each fighter and each other fighter, the damage of its best turn for every number of AP; empty for a fighter of its own side.</summary>
    public double[][][] DamageTable => _damage;

    /// <summary>Gets how far each fighter's spells reach, the longest.</summary>
    public int[] Range => _range;

    /// <summary>Gets whether each fighter moves.</summary>
    public bool[] Mobile => _mobile;

    /// <summary>Gets the most damage each fighter may deal an enemy in a turn.</summary>
    public double[] Hardest => _hardest;

    /// <summary>Gets the most damage any fighter may deal an enemy in a turn.</summary>
    public double HardestOfAll => _hardestOfAll;

    /// <summary>Gets the best turn's damage of a fighter on the enemy it hurts most, with some AP, wherever both stand.</summary>
    /// <param name="known">The fighter's place in the tables.</param>
    /// <param name="actionPoints">The AP.</param>
    /// <returns>The damage.</returns>
    public double TurnDamage(int known, int actionPoints)
    {
        // Its best turn's damage on the enemy it hurts most, with some AP, wherever both stand.
        var best = 0.0;

        foreach (var damage in _damage[known])
        {
            if (damage.Length > 0)
                best = Math.Max(best, damage[Math.Clamp(actionPoints, 0, MostPoints)]);
        }

        return best;
    }

    private double[] LandingTurns(int one, int other, ulong landing)
    {
        if (landing == All(_levels[one].Length))
            return _damage[one][other];

        if (!_landingTurns.TryGetValue((one, other, landing), out var turns))
            _landingTurns[(one, other, landing)] = turns = BestTurns(_levels[one], _levelDamage[one][other], landing);

        return turns;
    }

    /// <summary>Gets which of an attacker's damaging spells land on a target from a cell the attacker walks to.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="attacker">The attacker.</param>
    /// <param name="one">The attacker's place in the tables.</param>
    /// <param name="target">The target.</param>
    /// <param name="movementPoints">The MP the attacker walks with.</param>
    /// <returns>A bit for each spell that lands.</returns>
    public ulong Lands(ImaginedFight fight, Combatant attacker, int one, Combatant target, int movementPoints)
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

    /// <summary>Gets a fighter's place in the tables; a summon the search brought is found by its monster, its grade and its summoner.</summary>
    /// <param name="fighter">The fighter.</param>
    /// <returns>The place, or -1 when the tables do not know it.</returns>
    public int IndexOf(Combatant fighter)
    {
        var index = IndexOf(fighter.Id);

        if (index >= 0 || fighter is not { MonsterId: { } monster, Grade: { } grade, SummonerId: { } summoner })
            return index;

        return _summons.TryGetValue((monster, grade, summoner), out var summon) ? summon : -1;
    }

    /// <summary>Gets a fighter's place in the tables by its id.</summary>
    /// <param name="id">The fighter's id.</param>
    /// <returns>The place, or -1 when the tables do not know it.</returns>
    public int IndexOf(long id)
    {
        for (var index = 0; index < _fighters.Length; index++)
        {
            if (_fighters[index].Id == id)
                return index;
        }

        return -1;
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

    /// <summary>Gets the damage of the best turn of a fighter on another as the search left them, for every number of AP.</summary>
    /// <param name="one">The attacker's place in the tables.</param>
    /// <param name="attacker">The attacker as it is now.</param>
    /// <param name="other">The target's place in the tables.</param>
    /// <param name="target">The target as it is now.</param>
    /// <param name="landing">Which of the attacker's spells land on the target.</param>
    /// <returns>The damage by AP.</returns>
    public double[] Turns(int one, Combatant attacker, int other, Combatant target, ulong landing)
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
}
