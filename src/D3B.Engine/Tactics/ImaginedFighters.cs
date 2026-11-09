using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Tactics;

/// <summary>What a fight imagined does to a fighter and a cell, as pure functions of the fighters: damage through a shield, a heal, a characteristic changed, a free cell, the way a push goes (D192, 9f4).</summary>
internal static class ImaginedFighters
{
    public static bool InLife(EffectKind kind)
    {
        // What a critical hit changes by its numbers: the rest, a push or a state, is the normal cast's.
        return kind is EffectKind.Damage or EffectKind.FixedDamage or EffectKind.Steal or EffectKind.Heal or EffectKind.FixedHeal or EffectKind.PercentHeal
            or EffectKind.Shield or EffectKind.LifeShield or EffectKind.LevelShield or EffectKind.Boost or EffectKind.Deboost;
    }

    public static int IndexOf(List<Combatant> fighters, long id)
    {
        for (var index = 0; index < fighters.Count; index++)
        {
            if (fighters[index].Id == id)
                return index;
        }

        return -1;
    }

    public static bool IsFree(FightMap map, List<Combatant> fighters, MapPoint point)
    {
        return MapGeometry.TryGetCell(point, out var cell) && IsFree(map, fighters, cell);
    }

    public static bool IsFree(FightMap map, List<Combatant> fighters, int cell)
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

    public static Combatant Hurt(Combatant target, int damage)
    {
        if (damage <= 0 || !target.IsAlive)
            return target;

        var shield = target.Characteristic(FightCharacteristic.Shield);
        var absorbed = Math.Min(shield, damage);
        var life = target.Life - (damage - absorbed);
        var shielded = absorbed > 0 ? target with { Characteristics = target.Characteristics.SetItem(FightCharacteristic.Shield, shield - absorbed) } : target;

        return life > 0 ? shielded with { Life = life } : shielded with { Life = 0, IsAlive = false, Cell = Combatant.NoCell };
    }

    public static Combatant Healed(Combatant target, int heal)
    {
        return heal <= 0 || !target.IsAlive ? target : target with { Life = Math.Min(target.MaxLife, target.Life + heal) };
    }

    public static Combatant Changed(Combatant target, int characteristic, int change)
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

    public static (int X, int Y) Towards(MapPoint from, MapPoint to)
    {
        return (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
    }

    public static (int X, int Y) Along(MapPoint from, MapPoint to)
    {
        // Measured (9f4): a fighter out of line was pushed or pulled along the axis it stood farthest along, across only when
        // as far along both: 79 of 86 pulls on the cell imagined, against 57 going across, and 130 of 154 pushes against 120.
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);

        return Math.Abs(dx) == Math.Abs(dy) ? (Math.Sign(dx), Math.Sign(dy)) : Math.Abs(dx) > Math.Abs(dy) ? (Math.Sign(dx), 0) : (0, Math.Sign(dy));
    }
}
