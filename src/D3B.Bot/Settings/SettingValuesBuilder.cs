using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>Declares an activity's settings without a class, key by key, each with its default, such as from a script's table (D107).</summary>
/// <remarks>
/// The kinds, bounds and mechanisms are those of <see cref="ActivitySettingsBuilder{TSettings}"/> (D98), but for a choice,
/// whose values are the declaration's own strings rather than an enumeration's, and for conditions and rules, which a
/// declaration made of data does not hold. Settings show in the order they are declared, under the last section begun.
/// </remarks>
public sealed class SettingValuesBuilder
{
    private readonly List<SettingDefinition> _settings = [];
    private readonly Dictionary<string, object?> _defaults = new(StringComparer.Ordinal);
    private LocalizedText? _section;

    /// <summary>Begins a section: the settings declared next show under its title.</summary>
    /// <param name="title">The section's title, as the app shows it.</param>
    /// <returns>This builder.</returns>
    public SettingValuesBuilder Section(LocalizedText title)
    {
        ArgumentNullException.ThrowIfNull(title);

        _section = title;

        return this;
    }

    /// <summary>Declares a flag, on or off.</summary>
    /// <param name="key">The setting's key.</param>
    /// <param name="name">Its name, as the app shows it.</param>
    /// <param name="value">Its default.</param>
    /// <returns>The setting's builder.</returns>
    public SettingValueBuilder Flag(string key, LocalizedText name, bool value)
    {
        return Add(key, SettingKind.Flag, false, name, value);
    }

    /// <summary>Declares a whole number between bounds.</summary>
    /// <param name="key">The setting's key.</param>
    /// <param name="name">Its name, as the app shows it.</param>
    /// <param name="minimum">Its lowest value.</param>
    /// <param name="maximum">Its highest value.</param>
    /// <param name="value">Its default, within the bounds.</param>
    /// <returns>The setting's builder.</returns>
    public SettingValueBuilder Number(string key, LocalizedText name, int minimum, int maximum, int value)
    {
        if (maximum < minimum)
            throw new ArgumentException($"The setting {key}'s highest value is below its lowest.", nameof(maximum));

        return Add(key, SettingKind.Number, false, name, value, minimum, maximum);
    }

    /// <summary>Declares a text of bounded length.</summary>
    /// <param name="key">The setting's key.</param>
    /// <param name="name">Its name, as the app shows it.</param>
    /// <param name="maximumLength">Its greatest length.</param>
    /// <param name="value">Its default.</param>
    /// <returns>The setting's builder.</returns>
    public SettingValueBuilder Text(string key, LocalizedText name, int maximumLength, string value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        return Add(key, SettingKind.Text, false, name, value, maximumLength: maximumLength);
    }

    /// <summary>Declares a choice of one value among the declaration's own.</summary>
    /// <param name="key">The setting's key.</param>
    /// <param name="name">Its name, as the app shows it.</param>
    /// <param name="options">The values offered, each a string, with its name as the app shows it.</param>
    /// <param name="value">Its default, one of the values.</param>
    /// <returns>The setting's builder.</returns>
    public SettingValueBuilder Choice(string key, LocalizedText name, IReadOnlyList<SettingOption> options, string value)
    {
        return Add(key, SettingKind.Choice, false, name, value, options: Options(key, options));
    }

    /// <summary>Declares a choice of several values among the declaration's own.</summary>
    /// <param name="key">The setting's key.</param>
    /// <param name="name">Its name, as the app shows it.</param>
    /// <param name="options">The values offered, each a string, with its name as the app shows it.</param>
    /// <param name="values">Its default, values of the options.</param>
    /// <returns>The setting's builder.</returns>
    public SettingValueBuilder Choices(string key, LocalizedText name, IReadOnlyList<SettingOption> options, IReadOnlyList<string> values)
    {
        return Add(key, SettingKind.Choice, true, name, values, options: Options(key, options));
    }

    /// <summary>Declares a value of the game: a profession, an area, a sub-area or a resource, by its id.</summary>
    /// <param name="key">The setting's key.</param>
    /// <param name="kind">Which of the game's values.</param>
    /// <param name="name">Its name, as the app shows it.</param>
    /// <param name="value">Its default, or <see langword="null"/> for none.</param>
    /// <returns>The setting's builder.</returns>
    public SettingValueBuilder Game(string key, SettingKind kind, LocalizedText name, int? value)
    {
        return Add(key, GameKind(kind), false, name, value);
    }

    /// <summary>Declares several values of the game, by their ids.</summary>
    /// <param name="key">The setting's key.</param>
    /// <param name="kind">Which of the game's values.</param>
    /// <param name="name">Its name, as the app shows it.</param>
    /// <param name="values">Its default.</param>
    /// <returns>The setting's builder.</returns>
    public SettingValueBuilder Games(string key, SettingKind kind, LocalizedText name, IReadOnlyList<int> values)
    {
        return Add(key, GameKind(kind), true, name, values);
    }

    /// <summary>Ends the declaration.</summary>
    /// <returns>The settings, their defaults the declaration's.</returns>
    /// <exception cref="InvalidOperationException">A default is out of its setting's own bounds, or repeated.</exception>
    public ActivitySettings Build()
    {
        foreach (var setting in _settings)
            setting.Freeze();

        var all = _settings.ToList();
        var defaults = _defaults.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var settings = new ActivitySettings(typeof(SettingValues), () => Create(all, defaults), all, []);
        var problems = settings.Check(settings.CreateDefault())
            .Where(problem => problem.Kind is not SettingProblemKind.Required)
            .ToList();

        if (problems.Count > 0)
        {
            var wrong = string.Join(", ", problems.Select(problem => $"{problem.Setting?.Key} ({problem.Kind})"));

            throw new InvalidOperationException($"The default settings do not hold: {wrong}.");
        }

        return settings;
    }

    internal SettingDefinition Find(string key)
    {
        return _settings.Find(setting => setting.Key == key)
            ?? throw new InvalidOperationException($"The setting {key} is not declared before the one sought within it.");
    }

    private static SettingValues Create(IReadOnlyList<SettingDefinition> settings, Dictionary<string, object?> defaults)
    {
        var values = new SettingValues();

        foreach (var setting in settings)
            setting.SetValue(values, defaults[setting.Key]);

        return values;
    }

    private static SettingKind GameKind(SettingKind kind)
    {
        return kind is SettingKind.Job or SettingKind.Area or SettingKind.SubArea or SettingKind.Resource
            ? kind
            : throw new ArgumentException($"{kind} is not a value of the game.", nameof(kind));
    }

    private static List<SettingOption> Options(string key, IReadOnlyList<SettingOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0 || options.Any(option => option.Value is not string))
            throw new ArgumentException($"The choice {key} offers values, each a string.", nameof(options));

        if (options.Select(option => (string)option.Value).Distinct(StringComparer.Ordinal).Count() != options.Count)
            throw new ArgumentException($"The choice {key} offers a value twice.", nameof(options));

        return [.. options];
    }

    private SettingValueBuilder Add(
        string key,
        SettingKind kind,
        bool isList,
        LocalizedText name,
        object? value,
        int? minimum = null,
        int? maximum = null,
        int? maximumLength = null,
        IReadOnlyList<SettingOption>? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(name);

        if (_settings.Exists(setting => setting.Key == key))
            throw new ArgumentException($"The setting {key} is declared twice.", nameof(key));

        var setting = new SettingDefinition(
            key,
            settings => ((SettingValues)settings)[key],
            (settings, item) => ((SettingValues)settings).Set(key, item),
            kind,
            isList,
            name,
            _section,
            minimum,
            maximum,
            maximumLength,
            kind is SettingKind.Choice ? typeof(string) : null,
            options);

        // The default goes through the setting, which checks it is one of its kind.
        setting.SetValue(new SettingValues(), value);
        _settings.Add(setting);
        _defaults[key] = value;

        return new SettingValueBuilder(this, setting);
    }
}
