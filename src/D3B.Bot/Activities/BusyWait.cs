using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Activities;

/// <summary>An activity's wait, at its step's start, for a character busy or on a map not known yet: bounded (D484).</summary>
/// <remarks>
/// <para>
/// A step waits <see cref="Delay"/> at most, then the activity steps again. Nothing measured left a character busy so
/// long, but a walk the client never confirms and D3B did not send, a use whose end never comes, or a dialog left open
/// would make the activity wait forever, sending nothing, until the server closes the idle connection after 30 minutes.
/// </para>
/// <para>
/// So, busy for <see cref="Bound"/>, the character is put back in order as after an interruption (<see cref="Recovery"/>):
/// a use is let end, a walk settled, a dialog or exchange left, a walk the server holds let go. Still busy
/// <see cref="Bound"/> later, the activity fails, saying what held. The time counts from the first step that found the
/// character busy, until a step finds it free, an interruption comes, whose recovery puts it in order anyway, or the
/// steps stop a while, as the app's pause between two steps makes them.
/// </para>
/// </remarks>
public sealed class BusyWait
{
    private DateTimeOffset? _since;
    private DateTimeOffset? _last;
    private bool _settled;

    /// <summary>Gets how long a step waits for the character before the activity steps again.</summary>
    public static TimeSpan Delay { get; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets how long the character may stay busy before it is put back in order, then again before the activity fails.</summary>
    /// <remarks>Longer than anything a step leaves going: a harvest lasts seconds, a walk's confirmation less (D85).</remarks>
    public static TimeSpan Bound { get; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets how far apart two steps' starts lie at most while the activity steps, unpaused.</summary>
    /// <remarks>A wait of <see cref="Delay"/>, then <see cref="BotDelays.BetweenSteps"/>, 3 s at most: a longer gap is a pause.</remarks>
    public static TimeSpan Gap { get; } = TimeSpan.FromSeconds(20);

    /// <summary>Tells what keeps a character from a step's action, if anything.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>What holds it, or <see langword="null"/> when it is free, or fights, which no step waits for.</returns>
    public static BusyKind? Of(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.MapId is null)
            return BusyKind.MapUnknown;

        if (state.IsUsing)
            return BusyKind.Using;

        if (state.IsInDialog)
            return BusyKind.InDialog;

        return state.IsWalking ? BusyKind.Walking : null;
    }

    /// <summary>Waits for the character when it is busy or its map unknown, putting it back in order past the bound, failing past the next.</summary>
    /// <param name="context">The step's context.</param>
    /// <param name="failure">The activity's failure for its English message and the key of its text, which names what held.</param>
    /// <param name="cancellationToken">The step's token.</param>
    /// <returns>Whether the character was busy, so the step waited and must end for the activity to step again.</returns>
    /// <exception cref="ActivityException">The character stayed busy past the bound once put back in order.</exception>
    public async Task<bool> WaitAsync(BotContext context, Func<string, string, ActivityException> failure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(failure);

        var state = context.State;

        // A fight runs no step (the activity waits for its end, D88): one met here is the moment it began, and goes on alone.
        if (state.IsFighting)
        {
            Reset();
            await context.WaitUntilAsync(waited => !waited.IsBusy, Delay, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (Of(state) is not { } kind)
        {
            Reset();

            return false;
        }

        var now = context.Time.GetUtcNow();

        // A pause the app asks between two steps interrupts no behavior, so no interruption says it: the gap does.
        if (_last is { } last && now - last > Gap)
            Reset();

        _since ??= now;

        if (now - _since < Bound)
        {
            await context.WaitUntilAsync(waited => Of(waited) is null, Delay, cancellationToken).ConfigureAwait(false);
            _last = context.Time.GetUtcNow();

            return true;
        }

        if (_settled)
            throw Stuck(kind, failure);

        BotLog.ActivityBusy(context.Log, state.Key.CharacterId, kind);
        await Recovery.SettleAsync(context, cancellationToken).ConfigureAwait(false);
        _settled = true;
        _since = context.Time.GetUtcNow();
        _last = _since;

        return true;
    }

    /// <summary>Forgets how long the character was busy: an interruption came, whose recovery puts it back in order.</summary>
    public void Reset()
    {
        _since = null;
        _last = null;
        _settled = false;
    }

    private static ActivityException Stuck(BusyKind kind, Func<string, string, ActivityException> failure)
    {
        return kind switch
        {
            BusyKind.MapUnknown => failure("The character's map is still unknown after two minutes.", "FailStuckMapUnknown"),
            BusyKind.Using => failure("Still using an element after two minutes, even once put back in order.", "FailStuckUsing"),
            BusyKind.InDialog => failure("Still in a dialog or an exchange after two minutes, even once put back in order.", "FailStuckInDialog"),
            _ => failure("Still walking after two minutes, even once put back in order.", "FailStuckWalking"),
        };
    }
}
