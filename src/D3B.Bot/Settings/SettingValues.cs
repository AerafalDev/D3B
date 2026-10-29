namespace D3B.Bot.Settings;

/// <summary>The settings of an activity declared without a class, such as a script's: a value per setting's key (D107).</summary>
/// <remarks>
/// <see cref="SettingValuesBuilder"/> declares them, and their <see cref="SettingDefinition"/>s read and write them as a
/// class's properties are: the app edits them, a profile stores them, the activity receives them.
/// </remarks>
public sealed class SettingValues
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>Gets the keys that hold a value.</summary>
    public IEnumerable<string> Keys => _values.Keys;

    /// <summary>Gets a setting's value: a <see cref="bool"/>, an <see cref="int"/>, a <see cref="string"/>, an id of the game's, or an array for a list.</summary>
    /// <param name="key">The setting's key.</param>
    /// <returns>Its value, <see langword="null"/> when it has none.</returns>
    public object? this[string key] => _values.GetValueOrDefault(key);

    internal void Set(string key, object? value)
    {
        _values[key] = value;
    }
}
