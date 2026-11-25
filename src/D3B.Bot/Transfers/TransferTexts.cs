using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Transfers;

/// <summary>The transfers module's texts, in English, French and Spanish (<c>TransferTexts.resx</c>, <c>TransferTexts.fr.resx</c>, <c>TransferTexts.es.resx</c>), which the app shows in its language (D96).</summary>
public static class TransferTexts
{
    /// <summary>Gets the module's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Transfers.TransferTexts", typeof(TransferTexts).Assembly);

    /// <summary>Gets one of the module's texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }
}
