using D3B.Bot.Activities;

namespace D3B.Bot.Runtime;

/// <summary>A master's bank for its group, which its run asks before each step instead of its activity's own (D433, D441).</summary>
internal interface IGroupBank
{
    /// <summary>Tells whether the group's bank takes the activity's unloading: bots follow the bot, and the activity banks.</summary>
    /// <param name="activity">The bot's activity.</param>
    /// <returns>Whether it does.</returns>
    bool Takes(BotActivity activity);

    /// <summary>Tells whether a follower is full at the share of pods the activity banks at.</summary>
    /// <param name="activity">The bot's activity.</param>
    /// <returns>Whether one is.</returns>
    bool IsFull(BotActivity activity);

    /// <summary>Takes the group to the bank, has each follower give the bot what it carries, and deposits it with what the activity banks.</summary>
    /// <param name="context">The unloading's behavior context.</param>
    /// <param name="activity">The bot's activity.</param>
    /// <param name="cancellationToken">The unloading's token.</param>
    /// <returns>Whether the group was unloaded.</returns>
    Task<bool> UnloadAsync(BotContext context, BotActivity activity, CancellationToken cancellationToken);
}
