using D3B.Bot.Activities;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Activities;

/// <summary>An activity whose steps fail as a test says, each by its number from 1, and otherwise wait until cancelled.</summary>
internal sealed class FailingActivity : BotActivity
{
    private int _steps;
    private int _restarts;

    /// <summary>Gets or sets what a step throws, by its number, or null for a step that waits until cancelled; every step fails, by default.</summary>
    public Func<int, Exception?> Failure { get; init; } = _ => new InvalidOperationException("The step failed.");

    /// <summary>Gets or sets whether the run restarts the activity once it failed.</summary>
    public bool Restarts { get; init; } = true;

    /// <summary>Gets or sets what every interruption leads to.</summary>
    public InterruptionResponse Response { get; init; } = InterruptionResponse.Resume;

    /// <summary>Gets or sets until when the activity says it waits on purpose.</summary>
    public DateTimeOffset? Waiting { get; set; }

    /// <summary>Gets the number of steps started.</summary>
    public int Started => Volatile.Read(ref _steps);

    /// <summary>Gets the number of times the run began the activity anew.</summary>
    public int Restarted => Volatile.Read(ref _restarts);

    public override bool RestartsAfterFailure => Restarts;

    public override DateTimeOffset? WaitingUntil => Waiting;

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (Failure(Interlocked.Increment(ref _steps)) is { } failure)
            throw failure;

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        return true;
    }

    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        return Response;
    }

    public override void Restart()
    {
        Interlocked.Increment(ref _restarts);
    }
}
