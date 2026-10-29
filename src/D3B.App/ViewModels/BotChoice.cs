using D3B.Bot.Characters;

namespace D3B.App.ViewModels;

/// <summary>The bot the logs page and the protocol inspector look at, shared so that one follows the other (D111).</summary>
internal sealed class BotChoice
{
    /// <summary>Gets or sets the bot looked at last, if any.</summary>
    public CharacterKey? Key { get; set; }
}
