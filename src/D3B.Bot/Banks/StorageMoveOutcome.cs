namespace D3B.Bot.Banks;

/// <summary>How moving one stack into or out of an open storage ended (D551).</summary>
public enum StorageMoveOutcome
{
    /// <summary>The server moved it: the storage's stack and the weight came.</summary>
    Moved,

    /// <summary>No storage is open, or the stack named is not where it is taken from: nothing was sent.</summary>
    Busy,

    /// <summary>The character cannot carry it, or the destination cannot take it all: texts 285 and 266, nothing moved.</summary>
    TooHeavy,

    /// <summary>An object bound to the account, which no storage of another takes: text 356 (D153).</summary>
    NotExchangeable,

    /// <summary>The guild's rank lacks the right to open, put in or take out of the chest's tab, or the character is no member old enough of a guild.</summary>
    NoRight,

    /// <summary>The chest's tab does not take that type of object: text 661.</summary>
    TypeRefused,

    /// <summary>The server refused with another text of the error kind.</summary>
    Refused,

    /// <summary>The server did not answer in time, as the bank does a stack it does not take (D80).</summary>
    NoAnswer,
}
