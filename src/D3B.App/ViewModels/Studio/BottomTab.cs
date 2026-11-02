namespace D3B.App.ViewModels.Studio;

/// <summary>A tab of the bottom panel (D327).</summary>
internal enum BottomTab
{
    /// <summary>Every source by time.</summary>
    Journal,

    /// <summary>The decisions of the chosen character's run, from 7c4.</summary>
    Trace,

    /// <summary>The problems by origin, from 7c4.</summary>
    Problems,

    /// <summary>A character's frames live or a capture replayed.</summary>
    Protocol,

    /// <summary>The resources and runs by character and period.</summary>
    Statistics,
}
