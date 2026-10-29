using System.Text.Json;
using System.Text.Json.Nodes;

namespace D3B.Bot.Settings;

/// <summary>The settings an activity declares: their class, each setting, and the rules they hold together (D98).</summary>
/// <remarks>
/// <para>
/// A module describes them as it registers its activity
/// (<see cref="Activities.ActivityServiceCollectionExtensions.AddActivity{TActivity, TSettings}"/>), and the activity
/// receives an instance of their class through its constructor. The app shows each setting by its kind, checks them, and
/// a profile stores them as JSON, a value per setting's key.
/// </para>
/// <para>
/// Reading what was stored starts from the defaults, the class's own, then takes every value that still reads: a key
/// the activity no longer declares is dropped, and a value that no longer reads, such as a choice it renamed, keeps the
/// default and is reported.
/// </para>
/// </remarks>
public sealed class ActivitySettings
{
    private readonly Func<object>? _create;
    private readonly IReadOnlyList<SettingRule> _rules;

    /// <summary>Gets the settings of an activity that declares none.</summary>
    public static ActivitySettings None { get; } = new(null, null, [], []);

    /// <summary>Gets the settings' class, or <see langword="null"/> for an activity that declares none.</summary>
    public Type? Type { get; }

    /// <summary>Gets every setting, in the order the module declared them.</summary>
    public IReadOnlyList<SettingDefinition> All { get; }

    /// <summary>Gets whether the activity declares no setting.</summary>
    public bool IsEmpty => All.Count == 0;

    internal ActivitySettings(Type? type, Func<object>? create, IReadOnlyList<SettingDefinition> all, IReadOnlyList<SettingRule> rules)
    {
        Type = type;
        _create = create;
        All = all;
        _rules = rules;
    }

    private static bool Holds(SettingRule rule, object settings)
    {
        try
        {
            return rule.Holds(settings);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A plugin's rule runs in the app, which holds every client's connection (D91): one that throws does not hold.
            return false;
        }
    }

    /// <summary>Makes the default settings: the class's own values.</summary>
    /// <returns>A new instance of the settings' class, or <see langword="null"/> for an activity that declares none.</returns>
    public object? CreateDefault()
    {
        return _create?.Invoke();
    }

    /// <summary>Copies settings, such as for the app to edit them apart.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A new instance holding the same values.</returns>
    public object? Copy(object? settings)
    {
        CheckType(settings);

        if (settings is null)
            return null;

        var copy = _create!();

        foreach (var setting in All)
            setting.SetValue(copy, setting.GetValue(settings));

        return copy;
    }

    /// <summary>Reads settings stored as JSON.</summary>
    /// <param name="json">What was stored, or <see langword="null"/> for none.</param>
    /// <returns>The settings, and those whose stored value did not read.</returns>
    public SettingsReading Read(string? json)
    {
        var settings = CreateDefault();

        if (settings is null || string.IsNullOrWhiteSpace(json))
            return new SettingsReading(settings, []);

        JsonObject? values;

        try
        {
            values = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            values = null;
        }

        if (values is null)
            return new SettingsReading(settings, All);

        var reset = new List<SettingDefinition>();

        foreach (var setting in All)
        {
            if (values.TryGetPropertyValue(setting.Key, out var node) && !setting.TryRead(settings, node))
                reset.Add(setting);
        }

        return new SettingsReading(settings, reset);
    }

    /// <summary>Writes settings as JSON, to store them.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A JSON object, a value per setting's key.</returns>
    public string Write(object? settings)
    {
        CheckType(settings);

        var values = new JsonObject();

        if (settings is not null)
        {
            foreach (var setting in All)
                values[setting.Key] = setting.ToJson(settings);
        }

        return values.ToJsonString();
    }

    /// <summary>Tells whether a setting shows for settings, as the activity said.</summary>
    /// <param name="setting">One of these settings.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>Whether it shows.</returns>
    public bool IsVisible(SettingDefinition setting, object? settings)
    {
        ArgumentNullException.ThrowIfNull(setting);
        CheckType(settings);

        return settings is null || setting.IsVisible(settings);
    }

    /// <summary>Finds what is wrong with settings: each setting that shows, then the activity's rules.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="game">The game's values, to check those a setting names, or <see langword="null"/> to leave them unchecked.</param>
    /// <returns>A problem per setting at most, then one per rule that does not hold; none when the settings hold.</returns>
    public IReadOnlyList<SettingProblem> Check(object? settings, GameReferences? game = null)
    {
        CheckType(settings);

        if (settings is null)
            return [];

        var problems = new List<SettingProblem>();

        foreach (var setting in All)
        {
            if (setting.IsVisible(settings) && setting.Check(settings, game) is { } problem)
                problems.Add(problem);
        }

        foreach (var rule in _rules)
        {
            if (!Holds(rule, settings))
                problems.Add(new SettingProblem(null, SettingProblemKind.Rule, rule.Message));
        }

        return problems;
    }

    private void CheckType(object? settings)
    {
        if (Type is null ? settings is not null : !Type.IsInstanceOfType(settings))
            throw new ArgumentException($"The settings are a {Type?.Name ?? "nothing"}, not a {settings?.GetType().Name ?? "nothing"}.", nameof(settings));
    }
}
