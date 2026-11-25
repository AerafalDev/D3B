using System.Collections.Immutable;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;

namespace D3B.Bot.Activities;

/// <summary>What an activity does, rung after rung, before it tries again an action that failed, such as a journey or the bank: bounded, then the failure is the action's (D518).</summary>
/// <remarks>
/// <para>
/// Measured four times in a week (D481, D484, D488, D517): an action that failed once, the bank's journey refused at a mine's door,
/// a step waiting on a busy character, ended the activity or looped through the same maps, and the character idled until the
/// server closed the connection 30 minutes on. Each was mended case by case. The ladder is what any failure meets first.
/// </para>
/// <para>
/// Each failure climbs one rung: tried again twice from the state as it is, then once the character was put back in order as after an
/// interruption (a walk the server holds let go by a walk), then once it stepped aside, then once it left its map by the nearest exit,
/// whose information the client then asks for itself (D60). A rung's every wait is bounded, and a character still busy is waited for
/// <see cref="BusyWait.Delay"/> at most first. With no rung left, the caller fails, saying why. A success puts the ladder back at its foot.
/// </para>
/// </remarks>
public sealed class RecoveryLadder
{
    private static readonly ImmutableArray<RecoveryRung> _rungs = [RecoveryRung.Retry, RecoveryRung.Retry, RecoveryRung.PutInOrder, RecoveryRung.StepAside, RecoveryRung.LeaveMap];

    private int _climbed;

    /// <summary>Gets how many rungs the ladder has: as many failures in a row are tried again before the next ends the action.</summary>
    public static int Height => _rungs.Length;

    /// <summary>Gets how many rungs were climbed since the last success.</summary>
    public int Climbed => _climbed;

    /// <summary>Climbs the next rung once an action failed, so that it may be tried again.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="action">What failed, in English, for the logs.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>The rung climbed, or <see langword="null"/> when none is left: the action has failed for good.</returns>
    public async Task<RecoveryRung?> ClimbAsync(BotContext context, string action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);

        if (_climbed >= _rungs.Length)
            return null;

        var rung = _rungs[_climbed++];

        BotLog.Recovering(context.Log, context.State.Key.CharacterId, action, rung, _climbed, _rungs.Length);

        // A walk or a use going on ends by itself within seconds: no rung's request is sent through it.
        if (BusyWait.Of(context.State) is not null)
            await context.WaitUntilAsync(state => BusyWait.Of(state) is null, BusyWait.Delay, cancellationToken).ConfigureAwait(false);

        switch (rung)
        {
            case RecoveryRung.PutInOrder:
                await Recovery.SettleAsync(context, cancellationToken).ConfigureAwait(false);
                break;
            case RecoveryRung.StepAside:
                await context.StepAsideAsync(cancellationToken).ConfigureAwait(false);
                break;
            case RecoveryRung.LeaveMap:
                await context.LeaveByNearestExitAsync(cancellationToken).ConfigureAwait(false);
                break;
        }

        return rung;
    }

    /// <summary>Tries an action until it succeeds, climbing a rung after each failure, from the ladder's foot.</summary>
    /// <typeparam name="T">The action's outcome.</typeparam>
    /// <param name="context">The behavior's context.</param>
    /// <param name="action">What is tried, in English, for the logs.</param>
    /// <param name="attempt">The action.</param>
    /// <param name="succeeded">Tells an outcome that needs no other try.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>The first outcome that succeeded, or the last once no rung is left.</returns>
    public async Task<T> TryAsync<T>(BotContext context, string action, Func<CancellationToken, Task<T>> attempt, Func<T, bool> succeeded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(succeeded);

        Reset();

        while (true)
        {
            var outcome = await attempt(cancellationToken).ConfigureAwait(false);

            if (succeeded(outcome))
            {
                Reset();

                return outcome;
            }

            if (await ClimbAsync(context, action, cancellationToken).ConfigureAwait(false) is null)
                return outcome;
        }
    }

    /// <summary>Puts the ladder back at its foot: the action succeeded, or the activity begins anew.</summary>
    public void Reset()
    {
        _climbed = 0;
    }
}
