using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace D3B.Bot.Characters;

/// <summary>Who a bot plays: a character of an account, one bot each (D65).</summary>
/// <param name="AccountId">The account's id, as the connection phase's identification names it.</param>
/// <param name="CharacterId">The character's id, as the character choice names it.</param>
public readonly record struct CharacterKey(long AccountId, long CharacterId)
{
    /// <summary>Reads a character as a setting names it (<see cref="ToId"/>), such as a follower's master (D437).</summary>
    /// <param name="id">The text: the account's id and the character's, a colon between them.</param>
    /// <param name="key">The character, when the text names one.</param>
    /// <returns>Whether it does.</returns>
    public static bool TryParseId([NotNullWhen(true)] string? id, out CharacterKey key)
    {
        key = default;

        if (id?.Split(':') is not [var account, var character]
            || !long.TryParse(account, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId)
            || !long.TryParse(character, NumberStyles.None, CultureInfo.InvariantCulture, out var characterId))
        {
            return false;
        }

        key = new CharacterKey(accountId, characterId);

        return true;
    }

    /// <summary>Gets the character as a setting names it: the account's id and the character's, a colon between them (D437).</summary>
    /// <returns>The text, which <see cref="TryParseId"/> reads back.</returns>
    public string ToId()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{AccountId}:{CharacterId}");
    }
}
