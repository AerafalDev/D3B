using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using Microsoft.Extensions.Logging;

namespace MyPlugin;

/// <summary>The plugin's activity, which the app starts on a bot: steps chosen one after another from the character's state.</summary>
/// <remarks>
/// A step is a behavior's worth of work, chosen from the state as it is when the step starts: walking, travelling, using an
/// element or banking, through the context's behaviors (<c>WalkAsync</c>, <c>TravelAsync</c>, <c>UseAsync</c>,
/// <c>BankAsync</c>). An interruption, the player acting or the client closing, cancels the step; the next one starts from
/// the state again. Override <see cref="BotActivity.IsFull"/> and <see cref="BotActivity.UnloadAsync"/> to unload the
/// character when it carries too much.
/// </remarks>
public sealed class MyPluginActivity : BotActivity
{
    /// <inheritdoc/>
    public override Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;

        context.Log.LogInformation("{Name} stands on cell {Cell} of map {Map}.", state.Name, state.Cell, state.MapId);

        // Whether the activity goes on: false once it is done.
        return Task.FromResult(false);
    }
}
