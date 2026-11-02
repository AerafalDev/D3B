namespace D3B.Bot.Activities;

/// <summary>What an activity does once a gauge it watches reaches its threshold (D325).</summary>
public enum WatchAction
{
    /// <summary>It goes to unload at the bank, or the chest its settings name.</summary>
    Bank,

    /// <summary>It heals, eating or sitting.</summary>
    Heal,
}
