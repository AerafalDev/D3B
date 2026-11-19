namespace D3B.Engine.Tactics;

/// <summary>What a challenge asks of the movement points the fighter playing uses in its turn (D466).</summary>
public enum TurnMovement
{
    /// <summary>Nothing.</summary>
    Free,

    /// <summary>None used: Statue, its turn ending on the cell it began on.</summary>
    Still,

    /// <summary>Exactly one: Zombie.</summary>
    OnePoint,

    /// <summary>All of them: Nomade.</summary>
    AllPoints,
}
