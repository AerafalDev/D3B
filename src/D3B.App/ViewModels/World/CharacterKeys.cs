using System.Globalization;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.World;

/// <summary>Says a character's key as text, which the map, the Outliner and their rows name it by, and reads it back (D397).</summary>
internal static class CharacterKeys
{
    /// <summary>Says a key as <c>account:character</c>.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The text.</returns>
    public static string Text(CharacterKey key)
    {
        return key.AccountId.ToString(CultureInfo.InvariantCulture) + ":" + key.CharacterId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Reads a key said by <see cref="Text"/>.</summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>The key, or <see langword="null"/> when the text is none.</returns>
    public static CharacterKey? Parse(string? text)
    {
        var parts = text?.Split(':');

        return parts is { Length: 2 } && long.TryParse(parts[0], CultureInfo.InvariantCulture, out var account) && long.TryParse(parts[1], CultureInfo.InvariantCulture, out var character)
            ? new CharacterKey(account, character)
            : null;
    }
}
