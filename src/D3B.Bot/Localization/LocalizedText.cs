using System.Globalization;
using System.Resources;

namespace D3B.Bot.Localization;

/// <summary>A text the app shows in its language, French or English (D14): the same in both, one of a module's resources (D96), or a script's own by language (D107).</summary>
/// <remarks>
/// A module's resources are its own <c>.resx</c> files, the neutral one in English and a <c>.fr.resx</c> in French, read
/// through a <see cref="ResourceManager"/> of its assembly.
/// </remarks>
public sealed class LocalizedText
{
    private readonly string? _text;
    private readonly ResourceManager? _resources;
    private readonly IReadOnlyDictionary<string, string>? _languages;
    private readonly string _key;

    private LocalizedText(string? text, ResourceManager? resources, string key, IReadOnlyDictionary<string, string>? languages = null)
    {
        _text = text;
        _resources = resources;
        _key = key;
        _languages = languages;
    }

    /// <summary>Gets a text that is the same in every language.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    public static LocalizedText Invariant(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new LocalizedText(text, null, text);
    }

    /// <summary>Gets a text from resources, in the language the app asks for.</summary>
    /// <param name="resources">The resources, such as a module's own.</param>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text.</returns>
    public static LocalizedText From(ResourceManager resources, string key)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new LocalizedText(null, resources, key);
    }

    /// <summary>Gets a text written in several languages, such as a script's: the app's language's, else English, else the first.</summary>
    /// <param name="texts">The texts, by the language's two letters, such as <c>fr</c> and <c>en</c>.</param>
    /// <returns>The text.</returns>
    public static LocalizedText ByLanguage(IReadOnlyDictionary<string, string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0 || texts.Values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A text by language holds a text, not blank, in one language at least.", nameof(texts));

        var languages = texts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return new LocalizedText(null, null, texts.Values.First(), languages);
    }

    /// <summary>Gets a text of resources in a language, or its key between brackets when the resources lack it.</summary>
    /// <param name="resources">The resources.</param>
    /// <param name="key">The text's key.</param>
    /// <param name="culture">The language.</param>
    /// <returns>The text, or its key between brackets, as the app's own texts show a missing one.</returns>
    /// <remarks>
    /// Resources a module named wrong, such as under a folder its <c>.resx</c> moved to, throw for every text: the app reads
    /// them on its UI's thread, in the process that holds every client's connection (D91), where a text must not throw.
    /// </remarks>
    public static string Lookup(ResourceManager resources, string key, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);

        try
        {
            return resources.GetString(key, culture) ?? $"[{key}]";
        }
        catch (Exception exception) when (exception is MissingManifestResourceException or MissingSatelliteAssemblyException)
        {
            return $"[{key}]";
        }
    }

    /// <summary>Gets the text in a language.</summary>
    /// <param name="culture">The language.</param>
    /// <returns>The text, or its key between brackets when the resources lack it, or cannot be found.</returns>
    public string In(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (_languages is not null)
            return _languages.TryGetValue(culture.TwoLetterISOLanguageName, out var text) || _languages.TryGetValue("en", out text) ? text : _key;

        return _text ?? Lookup(_resources!, _key, culture);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return In(CultureInfo.CurrentUICulture);
    }
}
