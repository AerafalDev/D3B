namespace D3B.Data.Texts;

/// <summary>The languages packs hold texts in, by the code of the client's text files.</summary>
public static class TextLanguages
{
    /// <summary>French, the language the client's texts are written in.</summary>
    public const string French = "fr";

    /// <summary>English.</summary>
    public const string English = "en";

    /// <summary>Spanish.</summary>
    public const string Spanish = "es";

    /// <summary>Gets every language a pack holds.</summary>
    public static IReadOnlyList<string> All { get; } = [French, English, Spanish];

    /// <summary>Gets the language of the pack's texts that goes with a language of the app.</summary>
    /// <param name="twoLetterCode">The app's language, by its two-letter ISO code, such as <c>fr</c>.</param>
    /// <returns>The language when a pack holds it, else <see cref="English"/>.</returns>
    public static string Of(string twoLetterCode)
    {
        ArgumentNullException.ThrowIfNull(twoLetterCode);

        foreach (var language in All)
        {
            if (string.Equals(language, twoLetterCode, StringComparison.OrdinalIgnoreCase))
                return language;
        }

        return English;
    }
}
