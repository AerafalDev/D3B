using System.Collections.Immutable;
using D3B.Bot.Settings;

namespace D3B.Studio.Behaviors;

/// <summary>A node's values once compiled: those chosen and the profile's, fixed, and those a variable or a wire brings, read as it plays.</summary>
/// <remarks>
/// The fixed values are made once (D297): a node with nothing brought plays them as they are. One with something brought
/// plays a copy, so that the bot's loop and its steps never share one being filled.
/// </remarks>
internal sealed class BoundValues
{
    private readonly ActivitySettings _settings;
    private readonly object? _fixed;
    private readonly ImmutableArray<(SettingDefinition Setting, Func<Evaluation, object?> Source)> _brought;

    public BoundValues(ActivitySettings settings, object? values, ImmutableArray<(SettingDefinition Setting, Func<Evaluation, object?> Source)> brought)
    {
        _settings = settings;
        _fixed = values;
        _brought = brought;
    }

    public object? Resolve(Evaluation evaluation)
    {
        if (_brought.IsEmpty)
            return _fixed;

        var values = _settings.Copy(_fixed)!;

        foreach (var (setting, source) in _brought)
            setting.SetValue(values, source(evaluation));

        return values;
    }
}
