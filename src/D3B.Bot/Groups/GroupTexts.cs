using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Groups;

/// <summary>The groups module's texts, in English, French and Spanish (<c>GroupTexts.resx</c>, <c>GroupTexts.fr.resx</c>, <c>GroupTexts.es.resx</c>), which the app shows in its language (D96).</summary>
public static class GroupTexts
{
    /// <summary>Gets the module's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Groups.GroupTexts", typeof(GroupTexts).Assembly);

    /// <summary>Gets one of the module's texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }

    /// <summary>Says whom a master waits for before it acts, as its card shows it (D437).</summary>
    /// <param name="missing">The members missing, in the chain's order.</param>
    /// <returns>The text, or <see langword="null"/> when it waits for nobody.</returns>
    public static GameText? Waiting(IReadOnlyList<MissingMember> missing)
    {
        ArgumentNullException.ThrowIfNull(missing);

        return missing switch
        {
            [] => null,
            [var one] => GameText.Of(Text($"Waiting{one.Absence}"), GameTextValue.Of(one.Name)),
            _ => GameText.Of(Text("WaitingSeveral"), GameTextValue.Of(string.Join(", ", missing.Select(member => member.Name)))),
        };
    }
}
