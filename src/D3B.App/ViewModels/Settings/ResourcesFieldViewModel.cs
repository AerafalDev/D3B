using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>Resources of any profession, in their order: each with its profession and level, and a warning when a character playing the profile lacks it (D127); chosen several at once in a dialog (D159).</summary>
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

    /// <summary>Gets whether the dialog choosing resources can open: the editor shows in a window, and the host has the game's values.</summary>
    public bool CanChoose => Editor.Dialogs is not null && HasGame;

    /// <summary>Gets what choosing does: adding resources, or leaving them out (D165).</summary>
    public string ChooseText => Editor.Localizer[Definition.IsExcluding ? "ExcludeResources" : "AddResources"];

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        OnPropertyChanged(nameof(ChooseText));
    }

    /// <inheritdoc/>
    protected override string? WarningOf(object value)
    {
        // A resource left out is never harvested: no level it asks is lacking.
        return Definition.IsExcluding ? null : Editor.MissingLevelOf((int)value);
    }

    [RelayCommand]
    private void Choose()
    {
        if (Editor.Dialogs is { } dialogs)
            dialogs.Show(new ResourcePickerViewModel(dialogs, this, Editor, Available));
    }
}
