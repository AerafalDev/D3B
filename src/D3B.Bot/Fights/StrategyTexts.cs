using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Fights;

/// <summary>A fight's strategy's texts, in English and French (<c>StrategyTexts.resx</c>, <c>StrategyTexts.fr.resx</c>), which the app shows in its language (D96).</summary>
internal static class StrategyTexts
{
    /// <summary>Gets the strategy's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Fights.StrategyTexts", typeof(StrategyTexts).Assembly);

    /// <summary>Gets one of the strategy's texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }
}
