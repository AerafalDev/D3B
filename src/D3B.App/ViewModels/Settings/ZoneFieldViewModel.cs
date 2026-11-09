using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Settings;

/// <summary>A zone of the project, picked among the project's zones in a dialog, never typed (D415).</summary>
/// <remarks>
/// It says in plain words what a zone does for the activity, and how many maps the zone picked holds; a zone that holds none, or that
/// is gone, is a problem that blocks the profile. The dialog is the route's, with the zones' words.
/// </remarks>
internal sealed partial class ZoneFieldViewModel : SettingFieldViewModel, IPickingField
{
    /// <summary>Gets the id of the zone picked, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(HasValue), nameof(Detail))]
    public partial string? Value { get; private set; }

    /// <summary>Gets what the button says: the zone, else that none is picked.</summary>
    public string Text => Value is { } id ? NameOf(id) : Editor.Localizer["ZoneChoose"];

    /// <summary>Gets what is said under the field: what a zone does, with its map count once one is picked.</summary>
    public string? Detail => Value is { } id && MapsOf(id) is { Count: > 0 } maps
        ? $"{Editor.Localizer.Plural("RoutePickerMaps", maps.Count)}. {Help}"
        : Help;

    /// <summary>Gets whether a zone is picked.</summary>
    public bool HasValue => Value is not null;

    /// <summary>Gets whether a zone can be picked: the editor shows in a window and the project is known.</summary>
    public bool CanPick => Editor.Dialogs is not null && Editor.Library is not null;

    /// <inheritdoc/>
    public override bool HasInputProblem => ProblemOf() is not null;

    /// <inheritdoc/>
    ICommand IPickingField.PickCommand => PickCommand;

    /// <summary>Initializes a new instance of the <see cref="ZoneFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public ZoneFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = Read());
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Detail));
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        Value = Read();
        OnPropertyChanged(nameof(CanPick));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Detail));
    }

    /// <inheritdoc/>
    protected override string? InputProblem()
    {
        return ProblemOf();
    }

    [RelayCommand]
    private void Pick()
    {
        if (Editor.Dialogs is not { } dialogs || Editor.Library is not { } library)
            return;

        var localizer = Editor.Localizer;
        var choices = library.OfType<ZoneAsset>()
            .OrderBy(zone => zone.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(zone => (zone.Id, zone.Name, localizer.Plural("RoutePickerMaps", MapsOf(zone.Id)?.Count ?? 0), zone.Id == Value))
            .ToList();

        dialogs.Show(new RoutePickerViewModel(dialogs, localizer["ZonePickerTitle"], localizer["ZonePickerNone"], localizer["ZoneClear"], localizer["Cancel"], choices, Value is not null, Choose));
    }

    [RelayCommand]
    private void Clear()
    {
        Choose(null);
    }

    private string? Read()
    {
        return Definition.GetValue(Draft) is string { Length: > 0 } id ? id : null;
    }

    private IReadOnlySet<long>? MapsOf(string id)
    {
        return Editor.Zones is { } zones && zones.TryResolve(id, out var maps) ? maps : null;
    }

    private string? ProblemOf()
    {
        // An editor that cannot see the project's zones cannot tell a zone is gone, as a route's does not.
        if (Value is not { } id || Editor.Zones is null)
            return null;

        return MapsOf(id) switch
        {
            null => Editor.Localizer["ZoneNotInProject"],
            { Count: 0 } => Editor.Localizer["ZoneHoldsNoMap"],
            _ => null,
        };
    }

    private string NameOf(string id)
    {
        return Editor.Library?.Find<ZoneAsset>(id) is { } zone ? zone.Name : Editor.Localizer.Format("ZoneGone", id);
    }

    /// <summary>Picks a zone, or none, as the dialog and the profile's map do.</summary>
    /// <param name="zone">The zone's id, or <see langword="null"/> for none.</param>
    public void Choose(string? zone)
    {
        Value = zone;
        Write(zone ?? "");
    }
}
