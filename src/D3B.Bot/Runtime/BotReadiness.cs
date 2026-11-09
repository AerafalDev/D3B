namespace D3B.Bot.Runtime;

/// <summary>When a behavior may start.</summary>
internal enum BotReadiness
{
    /// <summary>Now, or never: it fails when the bot cannot act.</summary>
    Now,

    /// <summary>Once the bot can act on a map, out of a fight.</summary>
    OutOfFight,

    /// <summary>Once the bot can act in a fight not over.</summary>
    InFight,
}
