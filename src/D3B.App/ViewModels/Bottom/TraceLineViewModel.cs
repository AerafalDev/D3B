namespace D3B.App.ViewModels.Bottom;

/// <summary>A line of the trace: when, in which state, what the behavior weighed or played, and how it ended (D327).</summary>
/// <param name="Time">When, hours, minutes and seconds.</param>
/// <param name="State">The state, or the layer Always for what was weighed.</param>
/// <param name="Mark">What the state's dot says: the state playing, the layer Always, or a cut.</param>
/// <param name="Text">What was weighed or played, in the app's language.</param>
/// <param name="Outcome">How it ended, said short; empty when there is nothing to say.</param>
/// <param name="Ending">How it ended, which tints its pill.</param>
internal sealed record TraceLineViewModel(string Time, string State, TraceMark Mark, string Text, string Outcome, TraceEnding Ending)
{
    /// <summary>Gets whether the dot is the state playing's.</summary>
    public bool IsPlaying => Mark is TraceMark.Playing;

    /// <summary>Gets whether the dot is a cut's.</summary>
    public bool IsCut => Mark is TraceMark.Cut;

    /// <summary>Gets whether the line says how it ended.</summary>
    public bool HasOutcome => Outcome.Length > 0;

    /// <summary>Gets whether it ended well: played, done, or a threshold that holds.</summary>
    public bool IsGood => Ending is TraceEnding.Good;

    /// <summary>Gets whether it ended badly: interrupted or failed.</summary>
    public bool IsBad => Ending is TraceEnding.Bad;
}
