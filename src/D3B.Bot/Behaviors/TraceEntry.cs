using D3B.Bot.Activities;
using D3B.Bot.Localization;

namespace D3B.Bot.Behaviors;

/// <summary>A decision of a behavior's run, as the bot's trace keeps it for the app (D327).</summary>
/// <remarks>
/// The journal says what the bot did; the trace says what the behavior weighed and chose, and why, for the studio's trace
/// and 7g's history (D302). Lines name what they are about by the asset's own names and the nodes' sentences, which the app
/// says in its language.
/// </remarks>
/// <param name="At">When, by the bot's clock.</param>
/// <param name="Run">When the run began, which tells one run's lines from another's.</param>
/// <param name="Behavior">The name the behavior was started under.</param>
/// <param name="State">The state playing, by its name or its id; none when none holds.</param>
/// <param name="Kind">What the behavior did.</param>
/// <param name="Outcome">How it ended.</param>
public sealed record TraceEntry(DateTimeOffset At, DateTimeOffset Run, string Behavior, string? State, TraceKind Kind, TraceOutcome Outcome)
{
    /// <summary>Gets what a step played said it does, an activity's doing, if it says.</summary>
    public GameText? Doing { get; init; }

    /// <summary>Gets the sentence of the node a step played, or the name of the activity, when nothing more is said.</summary>
    public LocalizedText? Node { get; init; }

    /// <summary>Gets the name of the state an urgent cut was for, or of the rule played.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the threshold weighed.</summary>
    public ActivityWatch? Watch { get; init; }
}
