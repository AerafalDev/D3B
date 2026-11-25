namespace D3B.Bot.Journal;

/// <summary>An activity its run restarted, once it failed or made no progress a while (D519).</summary>
/// <param name="At">When the run took the failure.</param>
/// <param name="Activity">The activity's name.</param>
/// <param name="Restart">The restart's number in the last hour, from 1.</param>
/// <param name="Delay">How long the run waits before it, once the character is put back in order.</param>
/// <param name="Cause">What failed, in English.</param>
public sealed record ActivityRestartedEntry(DateTimeOffset At, string Activity, int Restart, TimeSpan Delay, string Cause) : JournalEntry(At, 0);
