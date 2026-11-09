using D3B.Bot.Characters;
using D3B.Bot.Fights;

namespace D3B.Studio.Behaviors;

/// <summary>What a behavior's conditions read: the character's state and what the bot knows beside it (D298).</summary>
/// <param name="State">The character's state.</param>
/// <param name="IsConnected">Whether a game connection plays the character.</param>
/// <param name="FightsEnded">How many fights the bot saw end, so that two views tell a fight ended between them.</param>
/// <param name="LastFightEnd">How the last fight the bot saw end ended for the character, if one did.</param>
/// <param name="Now">The time, by the bot's clock.</param>
/// <param name="Started">When the behavior's run started, by the bot's clock.</param>
public sealed record BehaviorView(CharacterState State, bool IsConnected, int FightsEnded, FightEnd? LastFightEnd, DateTimeOffset Now, DateTimeOffset Started)
{
    /// <summary>Gets how long the run has gone on.</summary>
    public TimeSpan RunTime => Now - Started;
}
