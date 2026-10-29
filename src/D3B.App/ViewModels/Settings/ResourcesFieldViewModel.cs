using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>Resources of any profession, as tiles in their order: each with its profession and level, and a warning when a character playing the profile lacks it (D127).</summary>
internal sealed partial class ResourcesFieldViewModel : GamesFieldViewModel
{
    /// <inheritdoc/>
    public override bool IsWide => true;

    /// <summary>Initializes a new instance of the <see cref="ResourcesFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public ResourcesFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
    }

    /// <inheritdoc/>
    protected override string? WarningOf(object value)
    {
        return Editor.MissingLevelOf((int)value);
    }
}
