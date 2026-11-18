using D3B.Bot.Runtime;

namespace D3B.Bot.Groups;

/// <summary>Waiting on several bots at once: until one of them changes as said, the group changes, or a delay passes (D87, D437).</summary>
internal static class GroupWaits
{
    /// <summary>Waits until a bot's condition holds, the groups change, or the delay passes.</summary>
    /// <param name="conditions">Each bot and what it waits for of it, tried by that bot's loop.</param>
    /// <param name="groupsChanged">The host's groups' next change as it was taken before the decision waited on (<see cref="GroupDirectory.Changed"/>), which ends the wait too.</param>
    /// <param name="time">The clock the delay goes by.</param>
    /// <param name="atMost">The delay, or <see langword="null"/> to wait as long as it takes.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>A task that completes once something changed, or the delay passed.</returns>
    public static async Task AnyAsync(IEnumerable<(BotActor Bot, Func<BotActor, bool> Condition)> conditions, Task groupsChanged, TimeProvider time, TimeSpan? atMost, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var waits = new List<Task> { groupsChanged };

        waits.AddRange(conditions.Select(wait => wait.Bot.WaitUntilAsync(wait.Condition, linked.Token)));

        if (atMost is { } delay)
            waits.Add(Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, time, linked.Token));

        // A bot stopped with the host gives its waiters up at once: the others still end the wait, and the stopping
        // interrupts the behavior waiting.
        for (var first = await Task.WhenAny(waits).ConfigureAwait(false); first.Exception?.InnerException is ObjectDisposedException && waits.Count > 1;)
        {
            waits.Remove(first);
            first = await Task.WhenAny(waits).ConfigureAwait(false);
        }

        // The others' waiters leave their bots' loops with the token.
        await linked.CancelAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
