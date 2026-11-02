namespace D3B.Bot.Journal;

/// <summary>A state of the behavior the bot runs, chosen from the state of the character (D297).</summary>
/// <param name="At">When.</param>
/// <param name="Behavior">The name the behavior was started under.</param>
/// <param name="State">The state's name, or its id when it has none; none when no state holds.</param>
public sealed record StateEnteredEntry(DateTimeOffset At, string Behavior, string? State) : JournalEntry(At, 0);
