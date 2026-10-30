using D3B.Bot.Characters;

namespace D3B.Bot.Fights;

/// <summary>Gives the strategy each character plays (D233): D3B's defaults, or the one D3B's database keeps for it.</summary>
/// <remarks>Asked as a fight starts and while the bot waits for one, from the bot's loop: it must be quick, and never wait.</remarks>
public interface IStrategySource
{
    /// <summary>Gets the strategy a character plays.</summary>
    /// <param name="character">The character.</param>
    /// <returns>Its strategy, which the caller never changes; D3B's defaults when it has none.</returns>
    StrategySettings Played(CharacterKey character);
}
