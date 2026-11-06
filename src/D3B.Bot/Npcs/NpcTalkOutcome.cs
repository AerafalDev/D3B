namespace D3B.Bot.Npcs;

/// <summary>How talking to an NPC through a chain of replies ended (D384).</summary>
public enum NpcTalkOutcome
{
    /// <summary>The chain was given: the dialog ended, or the character was moved to another map.</summary>
    Done,

    /// <summary>The character walks already, uses an element, is in a dialog or an exchange, or where it stands is not known yet: nothing was sent.</summary>
    Busy,

    /// <summary>The map's information did not place the NPC on the map: it cannot be talked to, and nothing was sent.</summary>
    Missing,

    /// <summary>The server refused the talk, or a question did not offer the reply the chain gave to it: the dialog was left.</summary>
    Refused,

    /// <summary>The server answered nothing in time.</summary>
    NoAnswer,

    /// <summary>The server moved the character to another map but did not say what is on it in time.</summary>
    NotPlaced,
}
