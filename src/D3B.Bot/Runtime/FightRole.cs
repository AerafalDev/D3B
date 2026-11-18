namespace D3B.Bot.Runtime;

/// <summary>A bot's part in the fights of its group (D431, D434).</summary>
internal enum FightRole
{
    /// <summary>Its fight is its own, a group's member's too, such as an aggression: it closes it, as before (D216).</summary>
    Alone,

    /// <summary>Bots follow it and the server locked its fight to its party: the fight stays open to the party (D435).</summary>
    Leads,

    /// <summary>It is in its master's fight: the options of its team are its master's.</summary>
    Follows,
}
