namespace D3B.Bot.Activities;

/// <summary>A gauge of the character an activity watches against a threshold (D325).</summary>
public enum WatchedGauge
{
    /// <summary>The pods carried, a share of the most: watched as it rises to the threshold.</summary>
    Pods,

    /// <summary>The life, a share of the most: watched as it falls to the threshold.</summary>
    Life,
}
