using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A cell of the map a node plays on, picked on the map's own cells in a dialog (D121, D377).</summary>
internal sealed partial class CellFieldViewModel : SettingFieldViewModel, IPickingField
{
    /// <summary>Gets the cell picked, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(HasValue))]
    public partial int? Value { get; private set; }

    /// <summary>Gets what the button says: the cell, else that none is picked.</summary>
    public string Text => Value is { } cell ? Editor.Localizer.Format("CellNumber", cell) : Editor.Localizer["CellChoose"];

    /// <summary>Gets whether a cell is picked.</summary>
    public bool HasValue => Value is not null;

    /// <summary>Gets whether the cell can be picked: the editor shows in a window, and the map is known.</summary>
    public bool CanPick => Editor.Dialogs is not null && Editor.Map is not null;

    /// <inheritdoc/>
    ICommand IPickingField.PickCommand => PickCommand;

    /// <summary>Initializes a new instance of the <see cref="CellFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public CellFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = definition.GetValue(Draft) as int?);
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        OnPropertyChanged(nameof(Text));
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        Value = Definition.GetValue(Draft) as int?;
        OnPropertyChanged(nameof(CanPick));
    }

    [RelayCommand]
    private void Pick()
    {
        if (Editor.Dialogs is not { } dialogs || Editor.Map is not { } map)
            return;

        dialogs.Show(new CellPickerViewModel(dialogs, map, Value, Editor.Localizer["CellPickerTitle"], Editor.Localizer["CellPickerHint"], Editor.Localizer["CellClear"], Editor.Localizer["Cancel"], Set));
    }

    [RelayCommand]
    private void Clear()
    {
        Set(null);
    }

    private void Set(int? cell)
    {
        Value = cell;
        Write(cell);
    }
}
