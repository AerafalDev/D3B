using System.Collections.Immutable;
using D3B.Data.Monsters;

namespace D3B.Engine.Fights;

/// <summary>A fighter as a fight's rules see it: where it stands, its team, what it has left, what it is, and its states.</summary>
/// <remarks>
/// The rules read it and never change it. Its characteristics are the server's, by id (client.md): AP 1, MP 23, range 19,
/// summons 26, dodge 78 and lock 79. A value the bot does not know is left at its default, and a rule asking it says so.
/// </remarks>
public sealed record Combatant
{
    /// <summary>The <see cref="Cell"/> of a fighter on no cell, such as a dead one.</summary>
    public const int NoCell = -1;

    /// <summary>Gets the fighter's id.</summary>
    public required long Id { get; init; }

    /// <summary>Gets the fighter's team, as the server numbers it.</summary>
    public int Team { get; init; }

    /// <summary>Gets the cell the fighter stands on, or <see cref="NoCell"/>.</summary>
    public int Cell { get; init; } = NoCell;

    /// <summary>Gets whether the fighter lives.</summary>
    public bool IsAlive { get; init; } = true;

    /// <summary>Gets whether the fighter is seen: every fighter captured was.</summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>Gets the AP the fighter has left.</summary>
    public int ActionPoints { get; init; }

    /// <summary>Gets the MP the fighter has left.</summary>
    public int MovementPoints { get; init; }

    /// <summary>Gets the fighter's range bonus, characteristic 19.</summary>
    public int RangeBonus { get; init; }

    /// <summary>Gets the fighter's lock, characteristic 79: how it tackles.</summary>
    public int Lock { get; init; }

    /// <summary>Gets the fighter's dodge, characteristic 78: how it escapes a tackle.</summary>
    public int Dodge { get; init; }

    /// <summary>Gets the fighter's life.</summary>
    public int Life { get; init; }

    /// <summary>Gets the fighter's most life.</summary>
    public int MaxLife { get; init; }

    /// <summary>Gets the most summons the fighter may have at once, characteristic 26, or null when not known.</summary>
    public int? MaxSummons { get; init; }

    /// <summary>Gets a character's breed, or null for a monster or a summon.</summary>
    public int? BreedId { get; init; }

    /// <summary>Gets a monster's or a summon's monster, or null for a character.</summary>
    public int? MonsterId { get; init; }

    /// <summary>Gets that monster's properties, from the pack: whether it moves and tackles, among others.</summary>
    public MonsterProperties MonsterProperties { get; init; }

    /// <summary>Gets who summoned the fighter, or null when it is no summon.</summary>
    public long? SummonerId { get; init; }

    /// <summary>Gets the states the fighter is in.</summary>
    public ImmutableArray<int> States { get; init; } = [];

    /// <summary>Gets whether the fighter is a summon.</summary>
    public bool IsSummon => SummonerId is not null;

    /// <summary>Gets whether the fighter is a player's character, no summon.</summary>
    public bool IsCharacter => BreedId is not null && SummonerId is null;

    /// <summary>Gets whether the fighter is a summon that cannot move, such as a turret.</summary>
    public bool IsStatic => IsSummon && MonsterId is not null && (MonsterProperties & MonsterProperties.CanMove) is 0;

    /// <summary>Tells whether the fighter is in a state.</summary>
    /// <param name="state">The state's id.</param>
    /// <returns>Whether it is.</returns>
    public bool HasState(int state)
    {
        return States.Contains(state);
    }
}
