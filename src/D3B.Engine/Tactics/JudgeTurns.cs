using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;

namespace D3B.Engine.Tactics;

/// <summary>What the fight's judge reckons of a fighter's turn that needs no state of its own: its effective life, the damage it deals for each number of AP, the signature of what its damage reads, and a buff taken away (D192, D207, D209).</summary>
internal static class JudgeTurns
{
    /// <summary>The most action points a turn is reckoned for.</summary>
    public const int MostPoints = 24;

    /// <summary>The most times a spell is cast in a reckoned turn.</summary>
    public const int MostCasts = 6;

    /// <summary>The number of a fighter's damaging spells whose landing the bits of a number tell.</summary>
    public const int Landings = 64;

    /// <summary>The characteristic of action points.</summary>
    public const int ActionPoints = 1;

    /// <summary>The characteristic of movement points.</summary>
    public const int MovementPoints = 23;

    public static double Effective(Combatant fighter)
    {
        return fighter.Life + Math.Max(0, fighter.Characteristic(FightCharacteristic.Shield));
    }

    public static double Nearing(int distance, int reach)
    {
        // Where no spell lands yet, each cell nearer still counts a little, so that the fighter playing comes to the enemies
        // it cannot strike yet instead of waiting where it stands.
        return 0.5 * Math.Pow(0.85, Math.Max(0, distance - reach - 1));
    }

    public static ulong All(int count)
    {
        return count >= Landings ? ulong.MaxValue : (1UL << count) - 1;
    }

    public static int Range(SpellLevel[] levels, Combatant fighter)
    {
        var range = 0;

        foreach (var level in levels)
            range = Math.Max(range, SpellRange.MaxRange(level, Math.Max(0, fighter.RangeBonus)));

        return range;
    }

    public static bool Damages(SpellCatalog spells, EffectActions actions, in SpellLevel level)
    {
        foreach (var effect in spells.EffectsOf(level))
        {
            if (actions.Of(effect.EffectId).Kind is EffectKind.Damage or EffectKind.Steal or EffectKind.FixedDamage)
                return true;
        }

        return false;
    }

    public static double[] BestTurns(SpellLevel[] levels, double[] damages, ulong landing)
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

    public static int Signature(Combatant fighter)
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

    public static Combatant Without(Combatant fighter, ImaginedBuff buff)
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
}
