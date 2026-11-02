namespace D3B.Bot.Journal;

/// <summary>A note a behavior writes, such as to warn of what it met (D298).</summary>
/// <param name="At">When.</param>
/// <param name="Behavior">The name the behavior was started under.</param>
/// <param name="Text">The note.</param>
public sealed record NoteEntry(DateTimeOffset At, string Behavior, string Text) : JournalEntry(At, 0);
