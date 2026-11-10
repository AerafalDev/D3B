using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;

namespace D3B.App.ViewModels.Settings;

/// <summary>A list of steps of the project, played as a step of another, picked among the project's lists in a dialog, never typed (D298, D421).</summary>
internal sealed partial class SequenceFieldViewModel : SettingFieldViewModel, IPickingField
{
    /// <summary>Gets the id of the list picked, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(HasValue))]
    public partial string? Value { get; private set; }

    /// <summary>Gets what the button says: the list, else that none is picked.</summary>
    public string Text => Value is { } id ? NameOf(id) : Editor.Localizer["SequenceChoose"];

    /// <summary>Gets whether a list is picked.</summary>
    public bool HasValue => Value is not null;

    /// <summary>Gets whether a list can be picked: the editor shows in a window and the project is known.</summary>
    public bool CanPick => Editor.Dialogs is not null && Editor.Library is not null;

    /// <inheritdoc/>
    ICommand IPickingField.PickCommand => PickCommand;

    /// <summary>Initializes a new instance of the <see cref="SequenceFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public SequenceFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = Read());
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
        Value = Read();
        OnPropertyChanged(nameof(CanPick));
    }

    [RelayCommand]
    private void Pick()
    {
        if (Editor.Dialogs is not { } dialogs || Editor.Library is not { } library)
            return;

        var localizer = Editor.Localizer;

        // A list that would play itself is not offered: the editor tells which those are.
        var choices = library.OfType<BehaviorAsset>()
            .Where(behavior => !behavior.IsGiven && BehaviorSequences.Read(behavior) is not null && (Editor.SequenceFilter?.Invoke(behavior) ?? true))
            .OrderBy(behavior => behavior.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(behavior => (behavior.Id, behavior.Name, localizer.Plural("SequenceStepsDetail", BehaviorSequences.Read(behavior)!.Steps.Length), behavior.Id == Value))
            .ToList();

        dialogs.Show(new RoutePickerViewModel(dialogs, localizer["SequencePickerTitle"], localizer["SequencePickerNone"], localizer["SequenceClear"], localizer["Cancel"], choices, Value is not null, Set));
    }

    [RelayCommand]
    private void Clear()
    {
        Set(null);
    }

    private string? Read()
    {
        return Definition.GetValue(Draft) is string { Length: > 0 } id ? id : null;
    }

    private string NameOf(string id)
    {
        return Editor.Library?.Find<BehaviorAsset>(id) is { } behavior ? behavior.Name : Editor.Localizer.Format("RouteGone", id);
    }

    private void Set(string? sequence)
    {
        Value = sequence;
        Write(sequence ?? "");
    }
}
