namespace D3B.Bot.Journal;

/// <summary>A line of a journal on disk: when it happened, and the text written, which starts with that time (D504).</summary>
/// <param name="At">When the entry or the problem happened: the day it is written in.</param>
/// <param name="Text">The line, without its line break.</param>
public readonly record struct JournalLine(DateTimeOffset At, string Text);
