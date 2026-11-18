namespace D3B.Bot.Groups;

/// <summary>Why a member of a group is not with the one waiting for it, which the bot's card says (D432, D437).</summary>
public enum MemberAbsence
{
    /// <summary>No game connection plays it: its client is closed or connecting again.</summary>
    Disconnected,

    /// <summary>The player took it over, or the app paused it.</summary>
    Paused,

    /// <summary>It is in a fight.</summary>
    Fighting,

    /// <summary>It is on another map, on its way or stuck there.</summary>
    Away,

    /// <summary>It is on the map, but not yet in the master's party.</summary>
    OutOfParty,

    /// <summary>It is with its master, its life under the share the master's profile heals under, which it regains before an attack (D433).</summary>
    Healing,
}
