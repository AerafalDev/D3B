using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Dungeons;

/// <summary>The dungeon activity's texts, in English and French (<c>DungeonTexts.resx</c>, <c>DungeonTexts.fr.resx</c>), which the app shows in its language (D96).</summary>
internal static class DungeonTexts
{
    /// <summary>Gets the activity's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Dungeons.DungeonTexts", typeof(DungeonTexts).Assembly);

    /// <summary>Gets one of the activity's texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }
}
