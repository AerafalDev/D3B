namespace D3B.Bot.Activities;

/// <summary>A restart of an activity by its run, once it failed or made no progress a while (D519).</summary>
/// <param name="At">When the run took the failure, by the bot's clock.</param>
/// <param name="Restart">The restart's number in the last hour, from 1.</param>
/// <param name="Delay">How long the run waited before it, once the character was put back in order.</param>
/// <param name="Cause">What failed: the activity's error, or the run's own when it made no progress or could not unload the character.</param>
public sealed record ActivityRestart(DateTimeOffset At, int Restart, TimeSpan Delay, Exception Cause);
