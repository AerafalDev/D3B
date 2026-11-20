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
/// Measured on the 49 fights captured (client.md), of 11 breeds and monsters: of the 396 casts the fighters chose, the first
/// of each of their sequences of a spell, 392 pass every check, a turret's needing the state the server gave it and 4 a
/// carrier's the state it carries in; the 4 others an Eliotrope cast through its portals, 9f's. The 581 others are cast by
/// effects, at the start of a fight or a turn, or by another cast, and are not checked. No capture holds a cast the server
/// refused:
/// which check refuses what is the references' idea until one does. Not checked, as not measured: the levels' global
/// cooldowns and casts per turn shared by a team, traps and portals on the cell, and the least character level, which
/// the server's own list of the character's spells answers.
/// </para>
/// <para>Built once per pack and shared: the criteria and masks it parses are kept, from any thread.</para>
/// </remarks>
public sealed class CastRules
{
    private const int BombSummon = 1008;
    private const int MaxBombs = 93;

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

        for (var index = 0; index < fighters.Count; index++)
        {
            var fighter = fighters[index];

            if (fighter.IsAlive && fighter.Cell is not Combatant.NoCell)
                cells.Add(fighter.Cell);
        }

        return cells;
    }

    /// <summary>Gets a level as a fighter casts it, changed by the modifiers its buffs give its spell.</summary>
    /// <param name="level">The level.</param>
    /// <param name="caster">The fighter casting it.</param>
    /// <returns>The level, its ranges, cost, interval, casts and conditions changed; its other fields kept.</returns>
    /// <remarks>
    /// The kinds are the pack's effects 280 to 299, as their texts describe them. Measured (client.md): a Pandawa's buffs give
    /// its carrying spell 3 more range, no taken cell and a free one; 3 casts of the captures carried a modifier of their
    /// spell, none needing it to pass. Sums apply in order, then a set interval or range; nothing falls below 0, and a count
    /// with no limit keeps none.
    /// </remarks>
    public static SpellLevel Modified(in SpellLevel level, Combatant caster)
    {
        ArgumentNullException.ThrowIfNull(caster);

        if (caster.Modifiers.IsDefaultOrEmpty)
            return level;

        int least = level.MinRange, most = level.Range, cost = level.ApCost, interval = level.MinCastInterval;
        int perTurn = level.MaxCastPerTurn, perTarget = level.MaxCastPerTarget;
        int? set = null, setRange = null, setMinRange = null;
        var properties = level.Properties;

        foreach (var modifier in caster.Modifiers)
        {
            if (modifier.SpellId != level.SpellId)
                continue;

            switch (modifier.Kind)
            {
                case SpellModifierKind.MinRange: least += modifier.Value; break;
                case SpellModifierKind.MinRangeLess: least -= modifier.Value; break;
                case SpellModifierKind.Range: most += modifier.Value; break;
                case SpellModifierKind.RangeLess: most -= modifier.Value; break;
                case SpellModifierKind.RangeBoostable: properties |= SpellLevelProperties.RangeCanBeBoosted; break;
                case SpellModifierKind.ApCost: cost -= modifier.Value; break;
                case SpellModifierKind.ApCostMore: cost += modifier.Value; break;
                case SpellModifierKind.Interval: interval -= modifier.Value; break;
                case SpellModifierKind.IntervalSet: set = modifier.Value; break;
                case SpellModifierKind.LineDisabled: properties &= ~SpellLevelProperties.CastInLine; break;
                case SpellModifierKind.LineOfSightDisabled: properties &= ~SpellLevelProperties.CastTestLos; break;
                case SpellModifierKind.CastsPerTurn when perTurn > 0: perTurn += modifier.Value; break;
                case SpellModifierKind.CastsPerTarget when perTarget > 0: perTarget += modifier.Value; break;
                case SpellModifierKind.TakenCellDisabled: properties &= ~SpellLevelProperties.NeedTakenCell; break;
                case SpellModifierKind.FreeCellDisabled: properties &= ~SpellLevelProperties.NeedFreeCell; break;
                case SpellModifierKind.FreeCellEnabled: properties |= SpellLevelProperties.NeedFreeCell; break;
                case SpellModifierKind.RangeSet: setRange = modifier.Value; break;
                case SpellModifierKind.MinRangeSet: setMinRange = modifier.Value; break;
            }
        }

        return level with
        {
            MinRange = (byte)Math.Clamp(setMinRange ?? least, 0, byte.MaxValue),
            Range = (byte)Math.Clamp(setRange ?? most, 0, byte.MaxValue),
            ApCost = (short)Math.Max(cost, 0),
            MinCastInterval = (byte)Math.Clamp(set ?? interval, 0, byte.MaxValue),
            MaxCastPerTurn = (sbyte)Math.Clamp(perTurn, sbyte.MinValue, sbyte.MaxValue),
            MaxCastPerTarget = (byte)Math.Clamp(perTarget, 0, byte.MaxValue),
            Properties = properties,
        };
    }

    private static bool Asks(Criterion? criterion, int state)
    {
        if (criterion is null)
            return false;

        var wanted = $"HS={state}";

        foreach (var part in criterion.Text.Split(['&', '|', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part == wanted)
                return true;
        }

        return false;
    }

    /// <summary>Gets a level as a fighter casts it among others: as its modifiers change it (<see cref="Modified"/>), and as a carrier throws.</summary>
    /// <param name="level">The level.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them.</param>
    /// <param name="caster">The fighter casting it.</param>
    /// <returns>The level as cast.</returns>
    /// <remarks>
    /// A level that throws (<see cref="Carrying.ThrowEffect"/>), cast by a fighter that carries, lands on a free cell, never a
    /// taken one, its range raised by what the level's own modifiers of its spell give the caster, unless the caster already holds
    /// a range modifier of that spell. Measured on 3.7.12.12 (D191): the server's buffs gave a carrier's Karcham 3 more range, no
    /// taken cell and a free one; on 3.7.14.18 the level holds that range for the client only, and no capture holds a throw (D474).
    /// </remarks>
    public SpellLevel Cast(in SpellLevel level, IReadOnlyList<Combatant> fighters, Combatant caster)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(caster);

        var cast = Modified(level, caster);

        if (!Throws(level) || !Carrying.Carries(fighters, caster.Id))
            return cast;

        var raised = 0;
        var spellId = level.SpellId;

        if (!caster.Modifiers.Any(modifier => modifier.SpellId == spellId && modifier.Kind is SpellModifierKind.Range or SpellModifierKind.RangeSet))
        {
            foreach (var effect in _spells.EffectsOf(level))
            {
                if (effect.EffectId == (int)SpellModifierKind.Range && effect.DiceNum == level.SpellId && MaskOf(effect).NamesTheCaster)
                    raised += effect.Value;
            }
        }

        return cast with
        {
            Range = (byte)Math.Clamp(cast.Range + raised, 0, byte.MaxValue),
            Properties = (cast.Properties & ~SpellLevelProperties.NeedTakenCell) | SpellLevelProperties.NeedFreeCell,
        };
    }

    /// <summary>Tells whether a level throws the fighter its caster carries.</summary>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <returns>Whether one of its effects is <see cref="Carrying.ThrowEffect"/>.</returns>
    public bool Throws(in SpellLevel level)
    {
        foreach (var effect in _spells.EffectsOf(level))
        {
            if (effect.EffectId is Carrying.ThrowEffect)
                return true;
        }

        return false;
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

        var cast = Cast(level, fighters, caster);
        var refusal = CasterRefusal(fighters, caster, level, cast, history, round);

        if (refusal is CastRefusal.None)
            refusal = CellRefusal(map, fighters, cast, history, round, cell);

        return refusal is CastRefusal.None ? ReachRefusal(map, fighters, caster, cast, caster.Cell, cell) : refusal;
    }

    /// <summary>Tells whether a fighter may cast a level on a cell, an open portal's sending it through the portals (D468).</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them.</param>
    /// <param name="marks">The marks on the fight's cells, its portals among them.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <param name="history">The caster's casts this fight.</param>
    /// <param name="round">The fight's round.</param>
    /// <returns>Why the cast is refused, or <see cref="CastRefusal.None"/>.</returns>
    /// <remarks>
    /// As the references check a cast through portals (D3): the portal within the level's range and sight of the caster, then
    /// what the level asks of a cell on the cell it comes out on, within its range and sight of the exit. A level needing a
    /// cell with no portal is refused on any portal's, the one cast on as the one it would land on: a Portail sets no portal
    /// through portals.
    /// </remarks>
    public CastRefusal Check(FightMap map, IReadOnlyList<Combatant> fighters, IReadOnlyList<CellMark> marks, Combatant caster, in SpellLevel level, int cell, CastHistory history, int round)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(history);

        var cast = Cast(level, fighters, caster);

        if (Passage(marks, fighters, caster, level, cell) is not { } passage)
        {
            var refusal = Check(map, fighters, caster, level, cell, history, round);

            return refusal is CastRefusal.None && HasPortalOn(marks, cast, cell) ? CastRefusal.CellHasPortal : refusal;
        }

        var refused = CasterRefusal(fighters, caster, level, cast, history, round);

        if (refused is CastRefusal.None)
            refused = CellRefusal(map, fighters, cast, history, round, passage.Target);

        if (refused is CastRefusal.None && (HasPortalOn(marks, cast, cell) || HasPortalOn(marks, cast, passage.Target)))
            refused = CastRefusal.CellHasPortal;

        if (refused is CastRefusal.None)
            refused = ReachRefusal(map, fighters, caster, cast, caster.Cell, cell);

        return refused is CastRefusal.None ? ReachRefusal(map, fighters, caster, cast, passage.Exit, passage.Target) : refused;
    }

    /// <summary>Finds the way a level cast on a cell goes through the portals, as <see cref="Portals.Through"/> finds it for any level that may go.</summary>
    /// <param name="marks">The marks on the fight's cells, its portals among them.</param>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <returns>The way, or none: the cell is no open portal's, or the level forbids the way through portals, or closes them.</returns>
    public PortalPassage? Passage(IReadOnlyList<CellMark> marks, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, int cell)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(caster);

        if (marks.Count is 0 || (Modified(level, caster).Properties & SpellLevelProperties.PortalProjectionForbidden) is not 0)
            return null;

        foreach (var effect in _spells.EffectsOf(level))
        {
            if (effect.EffectId is Portals.CloseEffect)
                return null;
        }

        return Portals.Through(marks, fighters, caster.Cell, cell);
    }

    private static bool HasPortalOn(IReadOnlyList<CellMark> marks, in SpellLevel cast, int cell)
    {
        if ((cast.Properties & SpellLevelProperties.NeedCellWithoutPortal) is 0)
            return false;

        for (var index = 0; index < marks.Count; index++)
        {
            if (marks[index].Kind is CellMarkKind.Portal && marks[index].Centre == cell)
                return true;
        }

        return false;
    }

    private static CastRefusal CellRefusal(FightMap map, IReadOnlyList<Combatant> fighters, in SpellLevel cast, CastHistory history, int round, int cell)
    {
        if (!map.IsWalkable(cell))
            return CastRefusal.CellNotWalkable;

        var occupant = fighters.FirstOrDefault(fighter => fighter.IsAlive && fighter.Cell == cell);
        var properties = cast.Properties;

        if ((properties & SpellLevelProperties.NeedFreeCell) is not 0 && occupant is not null)
            return CastRefusal.CellNotFree;

        if ((properties & SpellLevelProperties.NeedTakenCell) is not 0 && occupant is null)
            return CastRefusal.CellNotTaken;

        if ((properties & SpellLevelProperties.NeedVisibleEntity) is not 0 && occupant is not { IsVisible: true })
            return CastRefusal.TargetNotVisible;

        if (cast.MaxCastPerTarget > 0 && occupant is not null && history.CastsOn(cast.SpellId, occupant.Id, round) >= cast.MaxCastPerTarget)
            return CastRefusal.TooManyCastsOnTarget;

        return CastRefusal.None;
    }

    private static CastRefusal ReachRefusal(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel cast, int from, int cell)
    {
        if (!SpellRange.Reaches(cast, caster.RangeBonus, from, cell))
            return CastRefusal.OutOfRange;

        if ((cast.Properties & SpellLevelProperties.CastTestLos) is not 0 && !LineOfSight.IsClear(map, Occupied(fighters), from, cell))
            return CastRefusal.NoLineOfSight;

        return CastRefusal.None;
    }

    private CastRefusal CasterRefusal(IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, in SpellLevel cast, CastHistory history, int round)
    {
        if (!caster.IsAlive || caster.Cell is Combatant.NoCell)
            return CastRefusal.CasterAbsent;

        var spellId = level.SpellId;
        var criterion = CriterionOf(level);

        if (Prevented(caster, criterion))
            return CastRefusal.CasterCannotCast;

        if (caster.ActionPoints < cast.ApCost)
            return CastRefusal.NotEnoughActionPoints;

        if (!Holds(criterion, caster))
            return CastRefusal.StatesForbid;

        if (cast.InitialCooldown > 0 && round <= cast.InitialCooldown)
            return CastRefusal.InitialCooldown;

        if (cast.MinCastInterval > 0 && history.LastRounds.TryGetValue(spellId, out var last) && round - last < cast.MinCastInterval)
            return CastRefusal.Interval;

        if (cast.MaxCastPerTurn > 0 && history.CastsIn(spellId, round) >= cast.MaxCastPerTurn)
            return CastRefusal.TooManyCasts;

        return HasNoSlot(fighters, caster, cast) ? CastRefusal.TooManySummons : CastRefusal.None;
    }

    /// <summary>Tells whether a push or a pull moves a fighter: a monster the pack says may be pushed, and no fighter in a state that keeps it from being moved or pushed.</summary>
    /// <param name="fighter">The fighter.</param>
    /// <returns>Whether it moves.</returns>
    /// <remarks>Measured (9f4): of the pushes and pulls the server answered with no slide where D3B foresaw one, the targets.</remarks>
    public bool CanBePushed(Combatant fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        if (fighter.MonsterId is not null && (fighter.MonsterProperties & MonsterProperties.CanBePushed) is 0)
            return false;

        foreach (var state in fighter.States)
        {
            if (Has(state, SpellStateProperties.CantBeMoved) || Has(state, SpellStateProperties.CantBePushed))
                return false;
        }

        return true;
    }

    /// <summary>Tells whether a fighter's states let it cast a level, wherever and whenever.</summary>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <param name="caster">The fighter.</param>
    /// <returns>Whether none of its states prevents casting but those the level asks for, and the level's criterion on states holds.</returns>
    /// <remarks>
    /// What a judge of a fight reads of a fighter's spells (D228): a Zobal casts those of the mask it wears, each asking the
    /// mask's state (D224), so that a mask is worth the spells it lets the Zobal cast.
    /// </remarks>
    public bool StatesAllow(in SpellLevel level, Combatant caster)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var criterion = CriterionOf(level);

        return !Prevented(caster, criterion) && Holds(criterion, caster);
    }

    /// <summary>Gets the cells a fighter may cast a level on.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <param name="history">The caster's casts this fight.</param>
    /// <param name="round">The fight's round.</param>
    /// <returns>The cells <see cref="Check(FightMap, IReadOnlyList{Combatant}, Combatant, in SpellLevel, int, CastHistory, int)"/> allows.</returns>
    public CellSet Targets(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, CastHistory history, int round)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var cells = default(CellSet);

        if (caster.Cell is Combatant.NoCell)
            return cells;

        foreach (var cell in SpellRange.Cells(Cast(level, fighters, caster), caster.RangeBonus, caster.Cell))
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
    /// <returns>Each fighter the effect may reach, with whether its mask selects it or cannot tell; those it does not are left out.
    /// A mask naming the caster reaches it wherever it stands (D195).</returns>
    public IReadOnlyList<(Combatant Fighter, CriterionAnswer Selected)> Reached(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellEffect effect, int cell)
    {
        ArgumentNullException.ThrowIfNull(fighters);

        var reached = new List<(Combatant, CriterionAnswer)>();

        Reached(map, fighters, Occupied(fighters), caster, effect, cell, reached);

        return reached;
    }

    /// <summary>Adds the fighters an effect of a level cast on a cell reaches to a list, as <see cref="Reached(FightMap, IReadOnlyList{Combatant}, Combatant, in SpellEffect, int)"/> gives them.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them.</param>
    /// <param name="occupied">The cells the living fighters stand on, as <see cref="OccupiedBy"/> gives them.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="effect">The effect, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <param name="reached">The list the fighters reached are added to.</param>
    /// <param name="throughPortal">Whether the cast goes through a portal, its zone then directed from the exit, the caster's cell given as it.</param>
    /// <returns>The cells the effect's zone covers.</returns>
    /// <remarks>A search foresees many casts on one fight: the occupied cells are reckoned once, and nothing is allocated (D200).</remarks>
    public CellSet Reached(FightMap map, IReadOnlyList<Combatant> fighters, in CellSet occupied, Combatant caster, in SpellEffect effect, int cell, List<(Combatant Fighter, CriterionAnswer Selected)> reached, bool throughPortal = false)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(reached);

        // A caster on no cell, such as one carried, casts what its effects trigger from the cell cast on, with no direction.
        var from = caster.Cell is Combatant.NoCell ? cell : caster.Cell;
        var zone = ZoneCells.Of(effect.Zone, _spells.CellsOf(effect.Zone), map, occupied, from, cell);
        var mask = MaskOf(effect);
        var withTheDead = effect.Zone.Shape is SpellZoneShape.WholeMapWithTheDead;

        for (var index = 0; index < fighters.Count; index++)
        {
            var fighter = fighters[index];
            var named = fighter.Id == caster.Id && fighter.IsAlive && mask.NamesTheCaster;

            if (!named && (fighter.IsAlive ? !zone.Contains(fighter.Cell) : !withTheDead))
                continue;

            var selected = mask.Selects(caster, fighter, fighters, throughPortal);

            if (selected is not CriterionAnswer.False)
                reached.Add((fighter, selected));
        }

        return zone;
    }

    /// <summary>Gets the cells the living fighters stand on, where zones stop and lines of sight are blocked.</summary>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <returns>Their cells.</returns>
    public static CellSet OccupiedBy(IReadOnlyList<Combatant> fighters)
    {
        ArgumentNullException.ThrowIfNull(fighters);

        return Occupied(fighters);
    }

    /// <summary>Gets an effect's target mask, parsed once.</summary>
    /// <param name="effect">The effect, of the pack's spells.</param>
    /// <returns>Its mask.</returns>
    public TargetMask MaskOf(in SpellEffect effect)
    {
        return _masks.GetOrAdd(effect.TargetMask, static (index, spells) => TargetMask.Parse(spells.Strings.Span[index]), _spells);
    }

    private static bool Holds(Criterion? criterion, Combatant caster)
    {
        return criterion is null || criterion.Evaluate(new CriterionFacts { States = caster.States }) is CriterionAnswer.True;
    }

    private bool Prevented(Combatant caster, Criterion? criterion)
    {
        // A state that prevents casting lets through the levels that ask for it, as a carrier throws what it carries.
        foreach (var state in caster.States)
        {
            if (Has(state, SpellStateProperties.PreventsSpellCast) && !Asks(criterion, state))
                return true;
        }

        return false;
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
        if (_spells.TryGetSpell(level.SpellId, out var spell) && (spell.Properties & SpellProperties.BypassSummoningLimit) is not 0)
            return false;

        bool summons = false, bombs = false;

        foreach (var effect in _spells.EffectsOf(level))
        {
            summons |= _summons.Contains(effect.EffectId);
            bombs |= effect.EffectId is BombSummon;
        }

        // A Rogue's bombs have their own slots, as many as its characteristic 93 says: 3 on every character captured (9f3).
        if (bombs && caster.Characteristic(MaxBombs) is > 0 and var mostBombs
            && fighters.Count(fighter => fighter.IsAlive && fighter.SummonerId == caster.Id && (fighter.MonsterProperties & MonsterProperties.UseBombSlot) is not 0) >= mostBombs)
        {
            return true;
        }

        if (!summons || caster.MaxSummons is not { } most)
            return false;

        var count = fighters.Count(fighter => fighter.IsAlive && fighter.SummonerId == caster.Id && (fighter.MonsterProperties & MonsterProperties.UseSummonSlot) is not 0);

        return count >= most;
    }
}
