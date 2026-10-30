using System.Collections.Immutable;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Tactics;

/// <summary>What an action does to an imagined fight: a walk with its tackles, a cast with what its effects are foreseen to do.</summary>
/// <remarks>
/// <para>
/// A walk takes the MP and AP <see cref="FightMoves"/> says the path keeps. A cast spends its AP, counts in the caster's
/// history, then applies what <see cref="SpellEffects"/> foresees, in the level's order, each effect at the middle of its
/// range: damage taken from a shield first, then from life, a fighter dying at none; a steal healing its caster half; heals
/// up to the most life; shields; pushes and pulls along their line, stopped by what blocks them and hurting by the cells
/// left (<see cref="EffectFormulas.PushDamage"/>); swaps; boosts and deboosts, the AP and MP of the fighter playing at once
/// and the others' for their next turn; states set and removed; a spell's buffs dispelled; kills. An effect its mask only
/// maybe selects counts half, as does one of a random group, and one waiting for a trigger, such as a poison, counts half of
/// its damage. A critical hit is weighed by its chance (D207): what the level's effects give in life, damage, heals, shields
/// and characteristics, counts its share of a normal cast and of a critical hit; a push, a swap or a state, the normal
/// cast's alone. A boost, a deboost or a state that lasts is kept with its rounds (<see cref="ImaginedFight.Buffs"/>), which
/// the judge weighs over the turns it lasts (D209), and held by its fighter (<see cref="Combatant.Buffs"/>): one past its
/// level's stacks replaces the oldest of its spell and effect, as the server does (D227). The effects on a free cell, a
/// teleport of the caster and a summon, come from the level itself, the foreseen effects reaching only fighters.
/// </para>
/// <para>
/// A spell an effect casts at once on a fighter its mask surely selects is cast in turn, by whom and where
/// <see cref="EffectActions.CasterOf"/> says, its effects foreseen on the fight as the effects before left it, as deep as
/// a Zobal's masks chain them (D227): a mask takes the other masks' states off, puts its own on, then dispels the other
/// masks' buffs, each step reading the states the step before left. Such a spell's own critical hit is not weighed.
/// </para>
/// <para>Built once per pack and shared, from any thread.</para>
/// </remarks>
public sealed class Imagination
{
    private const double Maybe = 0.5;

    // Measured (D224): a Zobal's mask casts the spell that takes the masks' states off, which casts the grade that does it,
    // three deep; a spell casting itself stops here.
    private const int MostCastDepth = 4;

    // Summons the search imagines take ids no server gives: the server's are small negative numbers (client.md).
    private const long FirstImaginedSummon = -100_000;

    [ThreadStatic]
    private static List<ForeseenEffect>[]? _foreseen;

    private readonly SpellCatalog _spells;
    private readonly MonsterCatalog _monsters;
    private readonly EffectActions _actions;
    private readonly SpellEffects _effects;

    /// <summary>Initializes a new instance of the <see cref="Imagination"/> class.</summary>
    /// <param name="spells">The pack's spells.</param>
    /// <param name="monsters">The pack's monsters, for summons.</param>
    /// <param name="actions">What each effect does.</param>
    /// <param name="effects">What casts are foreseen to do.</param>
    public Imagination(SpellCatalog spells, MonsterCatalog monsters, EffectActions actions, SpellEffects effects)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(effects);

        _spells = spells;
        _monsters = monsters;
        _actions = actions;
        _effects = effects;
    }

    /// <summary>Gets the fight once the fighter playing walked to a cell it reaches.</summary>
    /// <param name="fight">The fight before.</param>
    /// <param name="cell">The cell, among those <see cref="FightMoves.Reachable"/> gives.</param>
    /// <param name="left">The MP and AP it keeps there.</param>
    /// <returns>The fight after the walk.</returns>
    public static ImaginedFight Walked(ImaginedFight fight, int cell, (int MovementPoints, int ActionPoints) left)
    {
        ArgumentNullException.ThrowIfNull(fight);

        return fight.With(fight.Actor with { Cell = cell, MovementPoints = left.MovementPoints, ActionPoints = left.ActionPoints });
    }

    private static int Expected(in ForeseenEffect effect, double share)
    {
        var middle = (effect.Value.Min + effect.Value.Max) / 2.0 * share;

        if (!effect.IsCertain)
            middle *= Maybe;

        if (effect.IsRandom)
            middle *= Maybe;

        return (int)Math.Round(middle, MidpointRounding.AwayFromZero);
    }

    private static bool InLife(EffectKind kind)
    {
        // What a critical hit changes by its numbers: the rest, a push or a state, is the normal cast's.
        return kind is EffectKind.Damage or EffectKind.FixedDamage or EffectKind.Steal or EffectKind.Heal or EffectKind.FixedHeal or EffectKind.PercentHeal
            or EffectKind.Shield or EffectKind.LifeShield or EffectKind.LevelShield or EffectKind.Boost or EffectKind.Deboost;
    }

    private static int IndexOf(List<Combatant> fighters, long id)
    {
        for (var index = 0; index < fighters.Count; index++)
        {
            if (fighters[index].Id == id)
                return index;
        }

        return -1;
    }

    private static bool IsFree(FightMap map, List<Combatant> fighters, int cell)
    {
        if (!map.IsWalkable(cell))
            return false;

        foreach (var fighter in fighters)
        {
            if (fighter.IsAlive && fighter.Cell == cell)
                return false;
        }

        return true;
    }

    private static Combatant Hurt(Combatant target, int damage)
    {
        if (damage <= 0 || !target.IsAlive)
            return target;

        var shield = target.Characteristic(FightCharacteristic.Shield);
        var absorbed = Math.Min(shield, damage);
        var life = target.Life - (damage - absorbed);
        var shielded = absorbed > 0 ? target with { Characteristics = target.Characteristics.SetItem(FightCharacteristic.Shield, shield - absorbed) } : target;

        return life > 0 ? shielded with { Life = life } : shielded with { Life = 0, IsAlive = false, Cell = Combatant.NoCell };
    }

    private static Combatant Healed(Combatant target, int heal)
    {
        return heal <= 0 || !target.IsAlive ? target : target with { Life = Math.Min(target.MaxLife, target.Life + heal) };
    }

    private static Combatant Changed(Combatant target, int characteristic, int change)
    {
        // The fighter playing spends its AP and MP now; the others hold those of their next turn.
        switch (characteristic)
        {
            case 1:
                return target with { ActionPoints = Math.Max(0, target.ActionPoints + change) };
            case 23:
                return target with { MovementPoints = Math.Max(0, target.MovementPoints + change) };
        }

        var value = EffectFormulas.ValueOf(target, characteristic) + change;
        var changed = target with { Characteristics = target.Characteristics.SetItem(characteristic, value) };

        return characteristic switch
        {
            19 => changed with { RangeBonus = value },
            78 => changed with { Dodge = value },
            79 => changed with { Lock = value },
            _ => changed,
        };
    }

    private static (int X, int Y) Towards(MapPoint from, MapPoint to)
    {
        return (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
    }

    /// <summary>Gets the fight once the fighter playing cast a level on a cell, which the rules allow.</summary>
    /// <param name="fight">The fight before.</param>
    /// <param name="level">The level, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <returns>The fight after the cast, as foreseen.</returns>
    public ImaginedFight Cast(ImaginedFight fight, in SpellLevel level, int cell)
    {
        ArgumentNullException.ThrowIfNull(fight);

        var caster = fight.Actor;
        var cost = CastRules.Modified(level, caster).ApCost;
        var target = fight.On(cell);
        var fighters = new List<Combatant>(fight.Fighters.Length + 1);
        var buffs = new List<ImaginedBuff>();
        var kept = fight.Buffs;

        fighters.AddRange(fight.Fighters);

        var casterIndex = IndexOf(fighters, caster.Id);

        fighters[casterIndex] = caster with { ActionPoints = Math.Max(0, caster.ActionPoints - cost) };
        Effects(fight.Map, fighters, casterIndex, level, cell, EffectFormulas.CriticalChance(level, caster), buffs, ref kept, 0);
        OnCell(fight.Map, fighters, casterIndex, level, cell);

        return fight with
        {
            Fighters = [.. fighters],
            History = fight.History.Cast(level.SpellId, target?.Id, fight.Round),
            Buffs = buffs.Count is 0 ? kept : kept.AddRange(buffs),
        };
    }

    private static List<ForeseenEffect> Foreseen(int depth)
    {
        // A spell an effect casts is foreseen while its caster's effects are still applied: a list for each depth.
        var lists = _foreseen ??= new List<ForeseenEffect>[MostCastDepth + 1];

        return lists[depth] ??= [];
    }

    private static Combatant Held(Combatant target, in SpellLevel level, in ForeseenEffect effect, int value)
    {
        return effect.Duration is 0 ? target : target with { Buffs = target.Buffs.Add(new HeldBuff(level.SpellId, effect.EffectId, value)) };
    }

    private static void Forget(List<ImaginedBuff> buffs, ref ImmutableArray<ImaginedBuff> kept, long fighterId, int spellId, int characteristic, int change)
    {
        // The search's lasting buffs of a spell on a fighter go with what took them back, an earlier cast's as this one's.
        bool Matches(ImaginedBuff buff)
        {
            return buff.FighterId == fighterId && (spellId is 0 || buff.SpellId == spellId) && buff.Characteristic == characteristic && buff.Change == change;
        }

        buffs.RemoveAll(Matches);

        if (kept.Any(Matches))
            kept = kept.RemoveAll(Matches);
    }

    private static void Lasting(List<ImaginedBuff> buffs, Combatant caster, Combatant target, in ForeseenEffect effect, int characteristic, int change, int spellId)
    {
        if (effect.Duration is not 0 && change is not 0)
            buffs.Add(new ImaginedBuff(caster.Id, target.Id, characteristic, change, effect.Duration, spellId));
    }

    private void Effects(FightMap map, List<Combatant> fighters, int casterIndex, in SpellLevel level, int cell, double chance, List<ImaginedBuff> buffs, ref ImmutableArray<ImaginedBuff> kept, int depth)
    {
        var foreseen = Foreseen(depth);
        var caster = fighters[casterIndex];

        foreseen.Clear();
        _effects.Foresee(map, fighters, caster, level, cell, false, foreseen);

        var normal = foreseen.Count;

        if (chance > 0)
            _effects.Foresee(map, fighters, caster, level, cell, true, foreseen);

        for (var index = 0; index < foreseen.Count; index++)
        {
            var effect = foreseen[index];
            var critical = index >= normal;

            if (critical && !InLife(effect.Action.Kind))
                continue;

            if (effect.Action.Kind is EffectKind.CastSpell)
                CastBy(map, fighters, casterIndex, effect, cell, buffs, ref kept, depth);
            else
                Apply(map, fighters, casterIndex, effect, cell, critical ? chance : InLife(effect.Action.Kind) ? 1 - chance : 1, level, buffs, ref kept);
        }

        foreseen.Clear();
    }

    private void CastBy(FightMap map, List<Combatant> fighters, int casterIndex, in ForeseenEffect effect, int cell, List<ImaginedBuff> buffs, ref ImmutableArray<ImaginedBuff> kept, int depth)
    {
        // A spell cast at once on a fighter the mask surely selects; one a trigger waits for, drawn, or only maybe cast is not.
        if (!effect.IsImmediate || !effect.IsCertain || effect.IsRandom || depth >= MostCastDepth)
            return;

        var targetIndex = IndexOf(fighters, effect.TargetId);

        if (targetIndex < 0 || !fighters[targetIndex].IsAlive || !_spells.TryGetLevel(effect.Value.Min, effect.Grade, out var level))
            return;

        var who = EffectActions.CasterOf(effect.EffectId);
        var by = who is SpellCaster.TargetOnItself or SpellCaster.TargetOnCell or SpellCaster.TargetOnCaster ? targetIndex : casterIndex;
        var at = who switch
        {
            SpellCaster.TargetOnItself or SpellCaster.CasterOnTarget => fighters[targetIndex].Cell,
            SpellCaster.TargetOnCell or SpellCaster.CasterOnCell => cell,
            _ => fighters[casterIndex].Cell,
        };

        if (fighters[by].IsAlive)
            Effects(map, fighters, by, level, at is Combatant.NoCell ? cell : at, 0, buffs, ref kept, depth + 1);
    }

    private void Apply(FightMap map, List<Combatant> fighters, int casterIndex, in ForeseenEffect effect, int cell, double share, in SpellLevel level, List<ImaginedBuff> buffs, ref ImmutableArray<ImaginedBuff> kept)
    {
        var index = IndexOf(fighters, effect.TargetId);

        if (index < 0)
            return;

        var target = fighters[index];
        var value = Expected(effect, share);

        if (!effect.IsImmediate)
        {
            // A buff waiting for its trigger, such as a poison at the target's turn, hurts later, if it ever triggers.
            if (effect.Action.Kind is EffectKind.Damage or EffectKind.Steal or EffectKind.FixedDamage)
                fighters[index] = Hurt(target, (int)(value * Maybe));

            return;
        }

        switch (effect.Action.Kind)
        {
            case EffectKind.Damage or EffectKind.FixedDamage:
                fighters[index] = Hurt(target, value);
                break;
            case EffectKind.Steal:
                fighters[index] = Hurt(target, value);
                fighters[casterIndex] = Healed(fighters[casterIndex], value / 2);
                break;
            case EffectKind.Heal or EffectKind.FixedHeal or EffectKind.PercentHeal:
                fighters[index] = Healed(target, value);
                break;
            case EffectKind.Shield or EffectKind.LifeShield or EffectKind.LevelShield:
                fighters[index] = target with { Characteristics = target.Characteristics.SetItem(FightCharacteristic.Shield, target.Characteristic(FightCharacteristic.Shield) + value) };
                break;
            case EffectKind.Push:
                Slide(map, fighters, casterIndex, index, cell, value, away: true);
                break;
            case EffectKind.Pull:
                Slide(map, fighters, casterIndex, index, cell, value, away: false);
                break;
            case EffectKind.Swap when target.IsAlive && fighters[casterIndex].Cell is not Combatant.NoCell:
                {
                    var from = fighters[casterIndex].Cell;

                    fighters[casterIndex] = fighters[casterIndex] with { Cell = target.Cell };
                    fighters[index] = target with { Cell = from };

                    break;
                }

            case EffectKind.Boost when effect.Action.Characteristic > 0:
                fighters[index] = Held(Changed(Stacked(target, level, effect, buffs, ref kept), effect.Action.Characteristic, value), level, effect, value);
                Lasting(buffs, fighters[casterIndex], target, effect, effect.Action.Characteristic, value, level.SpellId);
                break;
            case EffectKind.Deboost when effect.Action.Characteristic > 0:
                fighters[index] = Held(Changed(Stacked(target, level, effect, buffs, ref kept), effect.Action.Characteristic, -value), level, effect, value);
                Lasting(buffs, fighters[casterIndex], target, effect, effect.Action.Characteristic, -value, level.SpellId);
                break;
            case EffectKind.StateSet when !target.HasState(effect.Value.Min):
                fighters[index] = Held(target with { States = target.States.Add(effect.Value.Min) }, level, effect, effect.Value.Min);
                Lasting(buffs, fighters[casterIndex], target, effect, 0, effect.Value.Min, level.SpellId);
                break;
            case EffectKind.StateRemoved when target.HasState(effect.Value.Min):
                fighters[index] = Unstated(target, effect.Value.Min, buffs, ref kept);
                break;
            case EffectKind.DispelSpell:
                fighters[index] = Dispelled(target, effect.Value.Min, buffs, ref kept);
                break;
            case EffectKind.Kill:
                fighters[index] = target with { Life = 0, IsAlive = false, Cell = Combatant.NoCell };
                break;
        }
    }

    private Combatant Stacked(Combatant target, in SpellLevel level, in ForeseenEffect effect, List<ImaginedBuff> buffs, ref ImmutableArray<ImaginedBuff> kept)
    {
        // Measured (D224): a mask cast again, of 1 stack, had the server end its earlier boosts before giving them anew.
        if (effect.Duration is 0 || level.MaxStack <= 0)
            return target;

        var count = 0;
        var oldest = -1;

        for (var index = 0; index < target.Buffs.Length; index++)
        {
            if (target.Buffs[index].SpellId == level.SpellId && target.Buffs[index].EffectId == effect.EffectId && count++ is 0)
                oldest = index;
        }

        if (count < level.MaxStack)
            return target;

        var held = target.Buffs[oldest];

        Forget(buffs, ref kept, target.Id, level.SpellId, effect.Action.Characteristic, effect.Action.Kind is EffectKind.Deboost ? -held.Value : held.Value);

        return Undone(target, held) with { Buffs = target.Buffs.RemoveAt(oldest) };
    }

    private Combatant Unstated(Combatant target, int state, List<ImaginedBuff> buffs, ref ImmutableArray<ImaginedBuff> kept)
    {
        Forget(buffs, ref kept, target.Id, 0, 0, state);

        return target with
        {
            States = target.States.Remove(state),
            Buffs = target.Buffs.RemoveAll(held => held.Value == state && _actions.Of(held.EffectId).Kind is EffectKind.StateSet),
        };
    }

    private Combatant Dispelled(Combatant target, int spellId, List<ImaginedBuff> buffs, ref ImmutableArray<ImaginedBuff> kept)
    {
        // Measured (D224): a Zobal's new mask dispels the old one's buffs, whose states and characteristics go with them.
        var dispelled = target;

        foreach (var held in target.Buffs)
        {
            if (held.SpellId == spellId)
                dispelled = Undone(dispelled, held);
        }

        foreach (var buff in buffs.Concat(kept).Where(buff => buff.FighterId == target.Id && buff.SpellId == spellId).ToList())
            Forget(buffs, ref kept, target.Id, spellId, buff.Characteristic, buff.Change);

        return ReferenceEquals(dispelled, target) ? target : dispelled with { Buffs = target.Buffs.RemoveAll(held => held.SpellId == spellId) };
    }

    private Combatant Undone(Combatant target, HeldBuff held)
    {
        var action = _actions.Of(held.EffectId);

        return action.Kind switch
        {
            EffectKind.Boost when action.Characteristic > 0 => Changed(target, action.Characteristic, -held.Value),
            EffectKind.Deboost when action.Characteristic > 0 => Changed(target, action.Characteristic, held.Value),
            EffectKind.StateSet when !target.Buffs.Any(other => other != held && other.Value == held.Value && _actions.Of(other.EffectId).Kind is EffectKind.StateSet) => target with { States = target.States.Remove(held.Value) },
            EffectKind.SpellModifier => target with { Modifiers = target.Modifiers.Remove(new SpellModifier((SpellModifierKind)held.EffectId, held.Value, held.Parameter)) },
            _ => target,
        };
    }

    private static void Slide(FightMap map, List<Combatant> fighters, int casterIndex, int index, int cell, int cells, bool away)
    {
        var target = fighters[index];
        var caster = fighters[casterIndex];

        if (!target.IsAlive || target.Cell is Combatant.NoCell || cells <= 0)
            return;

        var at = MapGeometry.PointOf(target.Cell);
        var origin = target.Cell == cell && caster.Cell is not Combatant.NoCell ? MapGeometry.PointOf(caster.Cell) : MapGeometry.PointOf(cell);
        var (x, y) = away ? Towards(origin, at) : Towards(at, origin);

        if ((x, y) == (0, 0))
            return;

        var moved = 0;
        var position = target.Cell;

        while (moved < cells)
        {
            var next = new MapPoint(MapGeometry.PointOf(position).X + x, MapGeometry.PointOf(position).Y + y);

            // A pull stops beside the cell it pulls towards; anything else stops it short.
            if (!MapGeometry.TryGetCell(next, out var nextCell) || !IsFree(map, fighters, nextCell) || (!away && nextCell == cell))
                break;

            position = nextCell;
            moved++;
        }

        target = target with { Cell = position };

        if (away && moved < cells)
            target = Hurt(target, EffectFormulas.PushDamage(cells - moved, caster, caster.Level, target));

        fighters[index] = target;
    }

    private void OnCell(FightMap map, List<Combatant> fighters, int casterIndex, in SpellLevel level, int cell)
    {
        foreach (var effect in _spells.EffectsOf(level))
        {
            var kind = _actions.Of(effect.EffectId).Kind;

            if (effect.Delay is not 0 || (effect.Properties & SpellEffectProperties.ForClientOnly) is not 0 || !IsFree(map, fighters, cell))
                continue;

            if (kind is EffectKind.Teleport)
                fighters[casterIndex] = fighters[casterIndex] with { Cell = cell };
            else if (kind is EffectKind.Summon && Summoned(fighters, fighters[casterIndex], effect, cell) is { } summoned)
                fighters.Add(summoned);
        }
    }

    private Combatant? Summoned(List<Combatant> fighters, Combatant caster, in SpellEffect effect, int cell)
    {
        // Measured (D195): a summon's monster and grade are the effect's dice.
        var monsterId = effect.DiceNum;

        if (!_monsters.TryGetMonster(monsterId, out var monster) || !_monsters.TryGetGrade(monsterId, Math.Clamp(effect.DiceSide, 1, Math.Max(1, (int)monster.GradeCount)), out var grade))
            return null;

        var life = grade.Characteristics.LifePoints + grade.Characteristics.Vitality;

        return new Combatant
        {
            Id = FirstImaginedSummon - fighters.Count,
            Team = caster.Team,
            Cell = cell,
            Life = life,
            MaxLife = life,
            ActionPoints = grade.Characteristics.ActionPoints,
            MovementPoints = grade.Characteristics.MovementPoints,
            MonsterId = monsterId,
            MonsterProperties = monster.Properties,
            SummonerId = caster.Id,
            Level = grade.Level,
        };
    }
}
