using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>Completes a setting <see cref="SettingValuesBuilder"/> declared: its help, whether it needs a value, its order, what it is sought within.</summary>
public sealed class SettingValueBuilder
{
    private readonly SettingValuesBuilder _owner;
    private readonly SettingDefinition _setting;

    internal SettingValueBuilder(SettingValuesBuilder owner, SettingDefinition setting)
    {
        _owner = owner;
        _setting = setting;
    }

    /// <summary>Says what the app shows below the setting.</summary>
    /// <param name="help">The help, as the app shows it.</param>
    /// <returns>This builder.</returns>
    public SettingValueBuilder Help(LocalizedText help)
    {
        ArgumentNullException.ThrowIfNull(help);

        _setting.SetHelp(help);

        return this;
    }

    /// <summary>Asks for a value: a text not blank, a value of the game chosen, a list not empty.</summary>
    /// <returns>This builder.</returns>
    public SettingValueBuilder Required()
    {
        _setting.SetRequired();

        return this;
    }

    /// <summary>Says a list's order matters, such as a priority.</summary>
    /// <returns>This builder.</returns>
    public SettingValueBuilder Ordered()
    {
        _setting.SetOrdered();

        return this;
    }

    /// <summary>Seeks the setting's values within another's: a sub-area within an area, a resource within a profession.</summary>
    /// <param name="parent">The key of the setting declared before it, of one value.</param>
    /// <returns>This builder.</returns>
    public SettingValueBuilder Within(string parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parent);

        _setting.SetWithin(_owner.Find(parent));

        return this;
    }
}
