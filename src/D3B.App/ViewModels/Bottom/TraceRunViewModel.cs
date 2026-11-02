namespace D3B.App.ViewModels.Bottom;

/// <summary>A run of a behavior the trace may show: when it began, and what the toolbar calls it (D327).</summary>
/// <param name="Run">When it began, which tells its lines from another run's.</param>
/// <param name="Behavior">The name the behavior was started under.</param>
/// <param name="Label">What the toolbar calls it: the run going on, or the run of a time.</param>
internal sealed record TraceRunViewModel(DateTimeOffset Run, string Behavior, string Label)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        return Label;
    }
}
