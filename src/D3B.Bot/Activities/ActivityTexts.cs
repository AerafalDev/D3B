using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Activities;

/// <summary>The texts of an activity's run, in English and French (<c>ActivityTexts.resx</c>, <c>ActivityTexts.fr.resx</c>), which the app shows in its language (D96).</summary>
internal static class ActivityTexts
{
    /// <summary>Gets the run's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Activities.ActivityTexts", typeof(ActivityTexts).Assembly);

    /// <summary>Gets one of the run's texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }
}
