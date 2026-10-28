using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Activities;

/// <summary>What a bot does over time: steps chosen one after another from the character's state, written by a module or a plugin (D87).</summary>
/// <remarks>
/// <para>
/// A step is a behavior's worth of work, such as harvesting the nearest resource, going to the next map or banking, chosen
/// from the state as it is when the step starts. The bot runs the steps one after another (<see cref="BotActor.StartActivity"/>),
/// each as a behavior, so an interruption cancels the step in progress: the player acting, the game connection closing,
/// a fight beginning (D88), the host stopping. Once the bot can act again, the activity recovers the character
/// (<see cref="RecoverAsync"/>) and goes on with its next step: since steps start from the state, the activity resumes
/// where the state allows, and keeps no position of its own.
/// </para>
/// <para>
/// Every hook but <see cref="StepAsync"/> has a default, so that an activity overrides only what it needs: when the
/// character is full and how it is unloaded, what each interruption leads to, and how the character is put back in
/// order. An activity may as well decide everything in its steps.
/// </para>
/// </remarks>
public abstract class BotActivity
{
    /// <summary>Gets the activity's name, for the logs.</summary>
    public virtual string Name => GetType().Name;

    /// <summary>Runs the next step, chosen from the character's state now.</summary>
    /// <param name="context">The step's behavior context.</param>
    /// <param name="cancellationToken">The step's token: an interruption or the activity's stop cancels it.</param>
    /// <returns>Whether the activity goes on: <see langword="false"/> once it is done.</returns>
    public abstract Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken);

    /// <summary>Tells whether the character carries too much to go on, before each step: then the activity unloads it instead.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>Whether it is full; never, by default.</returns>
    public virtual bool IsFull(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return false;
    }

    /// <summary>Unloads the character, such as at its bank, once it is full.</summary>
    /// <param name="context">The unloading's behavior context.</param>
    /// <param name="cancellationToken">The unloading's token.</param>
    /// <returns>Whether the character was unloaded: by default it cannot be, and a full character ends the activity.</returns>
    public virtual Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(false);
    }

    /// <summary>Tells what an interruption leads to, once it cancelled a step; the host stopping always ends the activity.</summary>
    /// <param name="reason">Why the step stopped.</param>
    /// <returns>
    /// <see cref="InterruptionResponse.Resume"/> by default: the activity waits as long as it must for the bot to act again,
    /// resumed after the player, played again by a new connection of the client, out of the fight, then goes on.
    /// </returns>
    public virtual InterruptionResponse OnInterrupted(Interruption reason)
    {
        return InterruptionResponse.Resume;
    }

    /// <summary>Puts the character back in order once the bot can act again after an interruption, before the next step.</summary>
    /// <param name="context">The recovery's behavior context.</param>
    /// <param name="reason">Why the step stopped.</param>
    /// <param name="cancellationToken">The recovery's token.</param>
    /// <returns>A task that completes once the character is in order: by default as <see cref="Recovery.SettleAsync"/> leaves it.</returns>
    public virtual Task RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        return Recovery.SettleAsync(context, cancellationToken);
    }
}
