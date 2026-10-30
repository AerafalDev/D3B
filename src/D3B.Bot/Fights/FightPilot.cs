using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Engine.Tactics;

namespace D3B.Bot.Fights;

/// <summary>Has the AI play a bot's fights while it should: the host's switch on, its activity fighting its own, or, out of an activity, its strategy saying so (D198, D202, D235).</summary>
/// <remarks>
/// One per bot, from its making to its stop. It waits for a fight not over that the bot plays, connected and not paused,
/// then plays it (<see cref="FightPlay"/>) with the character's strategy as the fight starts, once the step it cut has
/// ended. The player's hand in the fight pauses the bot, and the fight is the player's; resumed from its card during the
/// fight, the bot plays it again. An AI that fails leaves the rest of that fight to the player.
/// </remarks>
internal sealed class FightPilot
{
    private readonly BotActor _bot;
    private readonly Lazy<FightTactics> _tactics;
    private readonly GameTables _tables;
    private readonly IStrategySource _strategies;

    /// <summary>Gets the pilot's run, over once the bot stops.</summary>
    public Task Run { get; }

    public FightPilot(BotActor bot, Lazy<FightTactics> tactics, GameTables tables, IStrategySource strategies)
    {
        _bot = bot;
        _tactics = tactics;
        _tables = tables;
        _strategies = strategies;
        Run = Task.Run(RunAsync);
    }

    private bool Playable(BotActor bot)
    {
        // The strategy is asked last, once a fight is there: it reads the database's memory, never waits.
        return bot.IsConnected && !bot.IsPaused && bot.State.Fight is { Phase: not FightPhase.Ended } && bot.Fights(_strategies.Played(bot.Key));
    }

    private async Task RunAsync()
    {
        while (true)
        {
            try
            {
                await _bot.WaitUntilAsync(Playable).ConfigureAwait(false);

                var strategy = _strategies.Played(_bot.Key);

                await _bot.RunInFightAsync((context, token) => FightPlay.PlayAsync(context, _tactics.Value, _tables, strategy, token), CancellationToken.None).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (BotInterruptedException exception) when (exception.Reason is Interruption.Stopped)
            {
                return;
            }
            catch (BotInterruptedException exception)
            {
                FightLog.Stopped(_bot.Log, _bot.Key.CharacterId, exception.Reason);

                continue;
            }
            catch (Exception exception)
            {
                FightLog.Failed(_bot.Log, _bot.Key.CharacterId, exception);
            }

            // Once over or failed, never twice in one fight: the next fight is the next to play.
            try
            {
                await _bot.WaitUntilAsync(bot => bot.State.Fight is not { Phase: not FightPhase.Ended }).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }
}
