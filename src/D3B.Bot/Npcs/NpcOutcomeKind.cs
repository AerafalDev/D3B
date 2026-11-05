namespace D3B.Bot.Npcs;

/// <summary>What follows a reply given to an NPC, or the action of talking to it, as the server showed it (D383).</summary>
public enum NpcOutcomeKind
{
    /// <summary>Another question: the target is its id.</summary>
    Question = 0,

    /// <summary>The dialog ends, the character staying on its map.</summary>
    End = 1,

    /// <summary>The character is moved to another map: the target is its id.</summary>
    MapChange = 2,

    /// <summary>An exchange opens, such as the bank's: the target is its type.</summary>
    Exchange = 3,
}
