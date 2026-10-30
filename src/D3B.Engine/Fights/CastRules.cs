using System.Collections.Concurrent;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Criteria;

namespace D3B.Engine.Fights;

/// <summary>Whether a fighter may cast a spell level on a cell, and which fighters each of its effects reaches.</summary>
/// <remarks>
/// <para>
/// The checks, in order, the references' (D181) as the pack's fields name them: the caster lives and stands on a cell, no
/// state of its prevents casting, it has the AP, the level's criterion on its states holds (<c>HS</c>, D71's syntax), the
/// level's first cast and its interval allow this round, it was not cast as many times as it may be this turn, a level
/// that summons leaves the caster a summon's slot; then the cell: one a fighter may stand on, free or taken as the level
/// asks, the fighter on it seen when asked, not cast on as many times as it may be this turn; then the range and the line
/// of sight (<see cref="SpellRange"/>, <see cref="LineOfSight"/>).
/// </para>
/// <para>
/// Measured on the 11 fights captured (client.md): the 79 casts the fighters chose, the first of each of their sequences of
/// a spell, pass every check, a turret's needing the state the server gave it. The 265 others are cast by effects, at the
/// start of a fight or a turn, or by another cast, and are not checked. No capture holds a cast the server refused:
/// which check refuses what is the references' idea until one does. Not checked, as not measured: the levels' global
/// cooldowns and casts per turn shared by a team, traps and portals on the cell, and the least character level, which
/// the server's own list of the character's spells answers.
/// </para>
/// <para>Built once per pack and shared: the criteria and masks it parses are kept, from any thread.</para>
/// </remarks>
public sealed class CastRules
{
    private static readonly int[] _summons = [181, 1011];

    private readonly SpellCatalog _spells;
    private readonly SpellStateCatalog _states;
    private readonly ConcurrentDictionary<int, Criterion> _criteria = new();
    private readonly ConcurrentDictionary<int, TargetMask> _masks = new();

    /// <summary>Initializes a new instance of the <see cref="CastRules"/> class.</summary>
    /// <param name="spells">The pack's spells.</param>
    /// <param name="states">The pack's states.</param>
    public CastRules(SpellCatalog spells, SpellStateCatalog states)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(states);

        _spells = spells;
        _states = states;
    }

    private static CellSet Occupied(IReadOnlyList<Combatant> fighters)
    {
        var cells = default(CellSet);

        foreach (var fighter in fighters)
        {
            if (fighter.IsAlive && fighter.Cell is not Combatant.NoCell)
                cells.Add(fighter.Cell);
        }

        return cells;
    }

    /// <summary>Tells whether a fighter may cast a level on a cell.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <param name="history">The caster's casts this fight.</param>
    /// <param name="round">The fight's round.</param>
    /// <returns>Why the cast is refused, or <see cref="CastRefusal.None"/>.</returns>
    public CastRefusal Check(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, int cell, CastHistory history, int round)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(history);

        if (!caster.IsAlive || caster.Cell is Combatant.NoCell)
            return CastRefusal.CasterAbsent;

        var spellId = level.SpellId;

        if (caster.States.Any(state => Has(state, SpellStateProperties.PreventsSpellCast)))
            return CastRefusal.CasterCannotCast;

        if (caster.ActionPoints < level.ApCost)
            return CastRefusal.NotEnoughActionPoints;

        if (CriterionOf(level) is { } criterion && criterion.Evaluate(new CriterionFacts { States = caster.States }) is not CriterionAnswer.True)
            return CastRefusal.StatesForbid;

        if (level.InitialCooldown > 0 && round <= level.InitialCooldown)
            return CastRefusal.InitialCooldown;

        if (level.MinCastInterval > 0 && history.LastRounds.TryGetValue(spellId, out var last) && round - last < level.MinCastInterval)
            return CastRefusal.Interval;

        if (level.MaxCastPerTurn > 0 && history.CastsIn(spellId, round) >= level.MaxCastPerTurn)
            return CastRefusal.TooManyCasts;

        if (HasNoSlot(fighters, caster, level))
            return CastRefusal.TooManySummons;

        if (!map.IsWalkable(cell))
            return CastRefusal.CellNotWalkable;

        var occupant = fighters.FirstOrDefault(fighter => fighter.IsAlive && fighter.Cell == cell);
        var properties = level.Properties;

        if ((properties & SpellLevelProperties.NeedFreeCell) is not 0 && occupant is not null)
            return CastRefusal.CellNotFree;

        if ((properties & SpellLevelProperties.NeedTakenCell) is not 0 && occupant is null)
            return CastRefusal.CellNotTaken;

        if ((properties & SpellLevelProperties.NeedVisibleEntity) is not 0 && occupant is not { IsVisible: true })
            return CastRefusal.TargetNotVisible;

        if (level.MaxCastPerTarget > 0 && occupant is not null && history.CastsOn(spellId, occupant.Id, round) >= level.MaxCastPerTarget)
            return CastRefusal.TooManyCastsOnTarget;

        if (!SpellRange.Reaches(level, caster.RangeBonus, caster.Cell, cell))
            return CastRefusal.OutOfRange;

        if ((properties & SpellLevelProperties.CastTestLos) is not 0 && !LineOfSight.IsClear(map, Occupied(fighters), caster.Cell, cell))
            return CastRefusal.NoLineOfSight;

        return CastRefusal.None;
    }

    /// <summary>Gets the cells a fighter may cast a level on.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <param name="history">The caster's casts this fight.</param>
    /// <param name="round">The fight's round.</param>
    /// <returns>The cells <see cref="Check"/> allows.</returns>
    public CellSet Targets(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, CastHistory history, int round)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var cells = default(CellSet);

        if (caster.Cell is Combatant.NoCell)
            return cells;

        foreach (var cell in SpellRange.Cells(level, caster.RangeBonus, caster.Cell))
        {
            if (Check(map, fighters, caster, level, cell, history, round) is CastRefusal.None)
                cells.Add(cell);
        }

        return cells;
    }

    /// <summary>Gets the fighters an effect of a level cast on a cell reaches: those in its zone its mask selects.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="effect">The effect, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <returns>Each fighter the effect may reach, with whether its mask selects it or cannot tell; those it does not are left out.</returns>
    public IReadOnlyList<(Combatant Fighter, CriterionAnswer Selected)> Reached(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellEffect effect, int cell)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(caster);

        var zone = SpellZones.Cells(effect.Zone, _spells.CellsOf(effect.Zone), map, Occupied(fighters), caster.Cell, cell);
        var mask = MaskOf(effect);
        var withTheDead = effect.Zone.Shape is SpellZoneShape.WholeMapWithTheDead;
        var reached = new List<(Combatant, CriterionAnswer)>();

        foreach (var fighter in fighters)
        {
            if (fighter.IsAlive ? !zone.Contains(fighter.Cell) : !withTheDead)
                continue;

            var selected = mask.Selects(caster, fighter, fighters);

            if (selected is not CriterionAnswer.False)
                reached.Add((fighter, selected));
        }

        return reached;
    }

    /// <summary>Gets an effect's target mask, parsed once.</summary>
    /// <param name="effect">The effect, of the pack's spells.</param>
    /// <returns>Its mask.</returns>
    public TargetMask MaskOf(in SpellEffect effect)
    {
        return _masks.GetOrAdd(effect.TargetMask, static (index, spells) => TargetMask.Parse(spells.Strings.Span[index]), _spells);
    }

    private bool Has(int state, SpellStateProperties property)
    {
        return _states.TryGetState(state, out var found) && (found.Properties & property) is not 0;
    }

    private Criterion? CriterionOf(in SpellLevel level)
    {
        if (level.StatesCriterion is SpellLevel.NoCriterion)
            return null;

        return _criteria.GetOrAdd(level.StatesCriterion, static (index, spells) => Criterion.Parse(spells.Strings.Span[index]), _spells);
    }

    private bool HasNoSlot(IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level)
    {
        if (caster.MaxSummons is not { } most)
            return false;

        if (_spells.TryGetSpell(level.SpellId, out var spell) && (spell.Properties & SpellProperties.BypassSummoningLimit) is not 0)
            return false;

        var summons = false;

        foreach (var effect in _spells.EffectsOf(level))
            summons |= _summons.Contains(effect.EffectId);

        if (!summons)
            return false;

        var count = fighters.Count(fighter => fighter.IsAlive && fighter.SummonerId == caster.Id && (fighter.MonsterProperties & MonsterProperties.UseSummonSlot) is not 0);

        return count >= most;
    }
}
