using D3B.App.Controls;

namespace D3B.App.ViewModels.Characters;

/// <summary>One of a character's latest decisions on its thread: what, why, when, and a dot of its tone (D325).</summary>
/// <param name="Text">What was done or decided.</param>
/// <param name="Why">Why, or what it came to, if said.</param>
/// <param name="Time">When, hours and minutes.</param>
/// <param name="Tone">Its tone: done, moved, a problem.</param>
internal sealed record DecisionViewModel(string Text, string? Why, string Time, Tone Tone)
{
    /// <summary>Gets whether it says why.</summary>
    public bool HasWhy => Why is { Length: > 0 };
}
