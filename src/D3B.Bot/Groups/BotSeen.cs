using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Groups;

/// <summary>A bot as it was seen when a decision was made, so that a wait begun after the decision misses no change made since (D436).</summary>
/// <param name="Bot">The bot.</param>
/// <param name="State">Its state then.</param>
/// <param name="IsConnected">Whether a game connection played it then.</param>
/// <param name="IsPaused">Whether it was paused then.</param>
internal sealed record BotSeen(BotActor Bot, CharacterState State, bool IsConnected, bool IsPaused)
{
    /// <summary>Sees a bot now: its connection and pause first, then its state.</summary>
    public static BotSeen Of(BotActor bot)
    {
        return new BotSeen(bot, bot.State, bot.IsConnected, bot.IsPaused);
    }

    /// <summary>Tells whether anything of the bot changed since it was seen: its state, whatever of it, its connection or its pause.</summary>
    public bool Changed(BotActor now)
    {
        return !ReferenceEquals(now.State, State) || now.IsConnected != IsConnected || now.IsPaused != IsPaused;
    }

    /// <summary>Tells whether the bot moved or its group's part changed since it was seen: its map, its placing, its haven bag, its party, its connection or its pause.</summary>
    public bool Moved(BotActor now)
    {
        var state = now.State;

        return state.MapId != State.MapId || state.Cell is null != State.Cell is null || state.HavenBagOwnerId != State.HavenBagOwnerId
            || state.Party != State.Party || state.Invitation != State.Invitation || state.IsFighting != State.IsFighting
            || now.IsConnected != IsConnected || now.IsPaused != IsPaused;
    }
}
