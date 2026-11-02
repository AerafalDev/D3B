namespace D3B.App.ViewModels.Bottom;

/// <summary>How a trace's line ended, as its pill is tinted (D327).</summary>
internal enum TraceEnding
{
    /// <summary>Nothing to tint: a threshold that does not hold.</summary>
    Neutral,

    /// <summary>Played, done, or a threshold that holds: green.</summary>
    Good,

    /// <summary>Interrupted or failed: red.</summary>
    Bad,
}
