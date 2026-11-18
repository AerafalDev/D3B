namespace D3B.Bot.Runtime;

/// <summary>A bot's part in the fights of its group (D431, D434).</summary>
internal enum FightRole
{
    /// <summary>Nobody follows it and it follows nobody: it closes its fight, as before (D216).</summary>
    Alone,

    /// <summary>Bots follow it: its fight stays open to its party, which the server locks it to.</summary>
    Leads,

    /// <summary>It follows a master: the options of its team are its master's.</summary>
    Follows,
}
