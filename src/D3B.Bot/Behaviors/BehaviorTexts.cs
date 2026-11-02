using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Behaviors;

/// <summary>The behaviors' texts, D3B's nodes' sentences among them, in English and French (<c>BehaviorTexts.resx</c>, <c>BehaviorTexts.fr.resx</c>), which the app shows in its language (D96).</summary>
internal static class BehaviorTexts
{
    /// <summary>Gets the texts' resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Behaviors.BehaviorTexts", typeof(BehaviorTexts).Assembly);

    /// <summary>Gets one of the texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }
}
