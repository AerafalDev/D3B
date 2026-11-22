using D3B.Engine.Fights;

namespace D3B.Engine.Tactics;

/// <summary>The Forgelance's lance, out of its hand once thrown, on the map until it dies or a cast kills it, then back in its hand (D465, D498).</summary>
/// <remarks>
/// <para>
/// A rule the pack does not hold, and the AI's only one that names a breed's states and monster, those of 3.7.14.18: no
/// effect of the pack takes the state a throw asks, the server does. Measured on the 83 game captures of 18 and 19 November 2026: at each throw the server ended states 3360 and 3590
/// on the Forgelance and gave it 3361; with the lance out, the players cast none of the spells asking 3360. That 304 of the
/// Forgelance's 308 turns began with 3360 and 3590 again and no lance on the map did not say the lance comes back as its
/// thrower's turn begins (D465): measured on the turns that ended with it on the map, it stays there (D498).
/// </para>
/// </remarks>
internal static class ForgelanceLance
{
    /// <summary>The lance, the monster each throw brings.</summary>
    public const int Monster = 7139;

    /// <summary>The lance in hand, which the throws ask.</summary>
    public const int InHand = 3360;

    /// <summary>The lance out of hand.</summary>
    public const int Out = 3361;

    /// <summary>The state the lance's coming takes from its thrower and its return gives back.</summary>
    public const int Carried = 3590;

    /// <summary>The spell the lance's coming casts on its thrower, setting the range of the spells cast on the lance (D489).</summary>
    /// <remarks>Read from the pack: the lance's starting spell casts it, and Armé, which the server casts as the lance returns, dispels it.</remarks>
    public const int Reach = 24591;

    /// <summary>Gets the thrower once its lance is out of its hand.</summary>
    /// <param name="thrower">The fighter that threw it.</param>
    /// <returns>The fighter without the lance in hand; the same when it did not hold it.</returns>
    public static Combatant Thrown(Combatant thrower)
    {
        if (!thrower.HasState(InHand))
            return thrower;

        var states = thrower.States.Remove(InHand).Remove(Carried);

        return thrower with { States = states.Contains(Out) ? states : states.Add(Out) };
    }

    /// <summary>Gives each Forgelance whose lance no longer lies on the map its lance back in hand, and takes off a thrower's older lances when a throw brought a new one.</summary>
    /// <param name="fighters">The fighters once an action is done, changed in place.</param>
    /// <param name="before">The fighters before it, which tell an older lance from the one the action brought.</param>
    /// <remarks>
    /// Measured on the captures of 18 to 22 November 2026 (D498): of the 221 Forgelance turns that ended with its lance on the
    /// map, 199 began the next with it there and its thrower without it in hand, the 22 others after an enemy killed it; a cast
    /// that kills it, a spell casting 30712 on it, left the thrower holding 3360 and 3590 at once; and the 17 throws of a
    /// thrower holding 3589 with its lance down all ended its turn with one lance, the new one.
    /// </remarks>
    public static void Recovered(List<Combatant> fighters, IReadOnlyList<Combatant> before)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(before);

        for (var index = 0; index < fighters.Count; index++)
        {
            var lance = fighters[index];

            if (!lance.IsAlive || lance.MonsterId != Monster || lance.SummonerId is not { } thrower || !Brought(lance.Id, before))
                continue;

            for (var other = 0; other < fighters.Count; other++)
            {
                if (fighters[other] is { IsAlive: true, MonsterId: Monster } older && older.SummonerId == thrower && WasThere(older.Id, before))
                    fighters[other] = older with { Life = 0, IsAlive = false, Cell = Combatant.NoCell };
            }
        }

        for (var index = 0; index < fighters.Count; index++)
        {
            var thrower = fighters[index];

            if (thrower.IsAlive && thrower.HasState(Out) && !fighters.Exists(fighter => fighter.IsAlive && fighter.MonsterId == Monster && fighter.SummonerId == thrower.Id))
                fighters[index] = Back(thrower);
        }
    }

    private static bool Brought(long id, IReadOnlyList<Combatant> before)
    {
        // A lance the action brought: not among the fighters alive before it.
        return !WasThere(id, before);
    }

    private static bool WasThere(long id, IReadOnlyList<Combatant> before)
    {
        foreach (var fighter in before)
        {
            if (fighter.Id == id && fighter.IsAlive)
                return true;
        }

        return false;
    }

    private static Combatant Back(Combatant thrower)
    {
        var states = thrower.States.Remove(Out);

        states = states.Contains(InHand) ? states : states.Add(InHand);
        states = states.Contains(Carried) ? states : states.Add(Carried);

        var modifiers = thrower.Modifiers;

        foreach (var held in thrower.Buffs.Where(held => held.SpellId == Reach))
            modifiers = modifiers.Remove(new SpellModifier((SpellModifierKind)held.EffectId, held.Value, held.Parameter));

        return thrower with { States = states, Modifiers = modifiers, Buffs = thrower.Buffs.RemoveAll(held => held.SpellId == Reach) };
    }
}
