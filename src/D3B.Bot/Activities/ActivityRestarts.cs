namespace D3B.Bot.Activities;

/// <summary>How a run restarts an activity whose profile asks it, once it failed or made no progress a while (D519).</summary>
/// <remarks>
/// Measured (D481, D484): the server closes a connection 30 minutes after the bot's last request. A run that made no progress for
/// <see cref="NoProgress"/>, then waits the longest delay before its restart, still sends a request within 18 minutes.
/// </remarks>
public sealed record ActivityRestarts
{
    /// <summary>Gets the restarts D3B makes.</summary>
    public static ActivityRestarts Default { get; } = new();

    /// <summary>Gets how long a run may go with no map changed, no fight begun and no pod gained or banked before it is taken for stuck.</summary>
    public TimeSpan NoProgress { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets how long the run waits before its first restart of the hour, doubled for each next one.</summary>
    public TimeSpan FirstDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the longest the run waits before a restart.</summary>
    public TimeSpan LongestDelay { get; init; } = TimeSpan.FromMinutes(8);

    /// <summary>Gets how many restarts an hour the run makes at most: one more failure, and it ends.</summary>
    public int MostPerHour { get; init; } = 6;

    /// <summary>Gets how often the run looks whether its activity made progress.</summary>
    public TimeSpan LookEvery { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets how long the run waits before a restart.</summary>
    /// <param name="restart">The restart's number in the last hour, from 1.</param>
    /// <returns>The first delay, doubled for each restart before it in the hour, the longest at most.</returns>
    public TimeSpan DelayOf(int restart)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(restart, 1);

        var delay = FirstDelay;

        for (var doubled = 1; doubled < restart && delay < LongestDelay; doubled++)
            delay += delay;

        return delay < LongestDelay ? delay : LongestDelay;
    }
}
