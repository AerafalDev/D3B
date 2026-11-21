using D3B.Data.Monsters;

namespace D3B.Engine.Fights;

/// <summary>A fighter carrying another, as a Pandawa's Karcham does, and throwing it onto a free cell (D474).</summary>
/// <remarks>
/// <para>
/// Measured on the night's capture of 2026-11-20: a carry (effect 50) put its carrier in state 3, <em>Porteur</em>, which
/// prevents casting but the levels asking it (D191), and the carried in states 8, <em>Porté</em>, and 3579, which halves the
/// damage it takes; the server set them itself with the carry, the pack's effects of those states being for the client only.
/// A throw (51) lands the carried on the cell cast on and ends both states (D191, D193).
/// </para>
/// <para>
/// Not measured, the references' model (D3, the owner's choice): the carried follows its carrier, a throw ends a carry, a blow
/// on the carrier's cell hits the carrier alone, and <c>K</c> names the fighter the caster carries or throws. A death ends one
/// there too, which the imagination leaves out, a turn never ending carrying but as a last resort.
/// </para>
/// </remarks>
public static class Carrying
{
    /// <summary>The effect that carries the fighter it reaches.</summary>
    public const int CarryEffect = 50;

    /// <summary>The effect that throws the fighter its caster carries onto the cell cast on.</summary>
    public const int ThrowEffect = 51;

    /// <summary>The state of a fighter carrying another, <em>Porteur</em>.</summary>
    public const int CarrierState = 3;

    /// <summary>The state of a fighter carried, <em>Porté</em>.</summary>
    public const int CarriedState = 8;

    /// <summary>The state the carried takes beside <see cref="CarriedState"/>, which halves the damage it takes.</summary>
    public const int HalvedState = 3579;

    /// <summary>Finds the fighter a fighter carries.</summary>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="carrierId">The carrier's id.</param>
    /// <returns>The index of the living fighter it carries, or -1.</returns>
    public static int CarriedBy(IReadOnlyList<Combatant> fighters, long carrierId)
    {
        ArgumentNullException.ThrowIfNull(fighters);

        for (var index = 0; index < fighters.Count; index++)
        {
            if (fighters[index].IsAlive && fighters[index].CarrierId == carrierId)
                return index;
        }

        return -1;
    }

    /// <summary>Tells whether a fighter carries another.</summary>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="carrierId">The fighter's id.</param>
    /// <returns>Whether a living fighter is carried by it.</returns>
    public static bool Carries(IReadOnlyList<Combatant> fighters, long carrierId)
    {
        return CarriedBy(fighters, carrierId) >= 0;
    }

    /// <summary>Tells whether a fighter may be carried: alive on a cell, carrying none and carried by none, and a monster the pack lets be; its states are <see cref="CastRules.StatesLetCarry"/>'s (D482).</summary>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="fighter">The fighter.</param>
    /// <returns>Whether it may.</returns>
    public static bool CanBeCarried(IReadOnlyList<Combatant> fighters, Combatant fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        if (!fighter.IsAlive || fighter.Cell is Combatant.NoCell || fighter.CarrierId is not null || fighter.HasState(CarrierState) || fighter.HasState(CarriedState))
            return false;

        if (fighter.MonsterId is not null && (fighter.MonsterProperties & MonsterProperties.CanBeCarried) is 0)
            return false;

        return !Carries(fighters, fighter.Id);
    }

    /// <summary>Gets two fighters once one carries the other, which <see cref="CanBeCarried"/> allows.</summary>
    /// <param name="carrier">The carrier.</param>
    /// <param name="carried">The fighter it carries.</param>
    /// <returns>Both as they become: the carried off its cell, both in their states.</returns>
    public static (Combatant Carrier, Combatant Carried) Carry(Combatant carrier, Combatant carried)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(carried);

        var states = carried.States;

        foreach (var state in (ReadOnlySpan<int>)[CarriedState, HalvedState])
        {
            if (!states.Contains(state))
                states = states.Add(state);
        }

        return (
            carrier.HasState(CarrierState) ? carrier : carrier with { States = carrier.States.Add(CarrierState) },
            carried with { Cell = Combatant.NoCell, CarrierId = carrier.Id, States = states });
    }

    /// <summary>Gets two fighters once a throw's effects are done: the thrown its carrier's no more, both out of their states.</summary>
    /// <param name="carrier">The carrier.</param>
    /// <param name="thrown">The fighter it threw, on the cell it landed on.</param>
    /// <returns>Both as they become.</returns>
    public static (Combatant Carrier, Combatant Thrown) Released(Combatant carrier, Combatant thrown)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(thrown);

        return (
            carrier with { States = carrier.States.Remove(CarrierState) },
            thrown with { CarrierId = null, States = thrown.States.Remove(CarriedState).Remove(HalvedState) });
    }
}
