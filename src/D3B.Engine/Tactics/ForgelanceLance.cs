using D3B.Engine.Fights;

namespace D3B.Engine.Tactics;

/// <summary>The Forgelance's lance, out of its hand once thrown and back in it as its next turn begins (D465).</summary>
/// <remarks>
/// <para>
/// A rule the pack does not hold, and the AI's only one that names a breed's states and monster, those of 3.7.14.18: no
/// effect of the pack takes the state a throw asks, the server does. Measured on the 83 game captures of 18 and 19 November 2026: at each throw the server ended states 3360 and 3590
/// on the Forgelance and gave it 3361; with the lance out, the players cast none of the spells asking 3360; 304 of the
/// Forgelance's 308 turns began with 3360 and 3590 again and no lance on the map.
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

    /// <summary>The spell the lance's coming casts on its thrower, setting the range of the spells cast on the lance (D487).</summary>
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

    /// <summary>Gets the fight once a fighter's lance came back to its hand, as its turn begins.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="fighterId">The fighter whose turn begins.</param>
    /// <returns>The fight without its lance on the map nor the reach it gave; the same when its lance was in its hand.</returns>
    public static ImaginedFight Returned(ImaginedFight fight, long fighterId)
    {
        if (fight.Find(fighterId) is not { IsAlive: true } thrower || !thrower.HasState(Out))
            return fight;

        var states = thrower.States.Remove(Out);

        states = states.Contains(InHand) ? states : states.Add(InHand);
        states = states.Contains(Carried) ? states : states.Add(Carried);

        var modifiers = thrower.Modifiers;

        foreach (var held in thrower.Buffs.Where(held => held.SpellId == Reach))
            modifiers = modifiers.Remove(new SpellModifier((SpellModifierKind)held.EffectId, held.Value, held.Parameter));

        var back = thrower with { States = states, Modifiers = modifiers, Buffs = thrower.Buffs.RemoveAll(held => held.SpellId == Reach) };

        return fight with { Fighters = [.. fight.Fighters.Where(fighter => !(fighter.MonsterId == Monster && fighter.SummonerId == fighterId)).Select(fighter => fighter.Id == fighterId ? back : fighter)] };
    }
}
