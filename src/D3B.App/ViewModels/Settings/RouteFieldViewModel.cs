using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Settings;

/// <summary>A route of the project, picked among the project's routes in a dialog, never typed (D298, D386).</summary>
internal sealed partial class RouteFieldViewModel : SettingFieldViewModel, IPickingField
{
    /// <summary>Gets the id of the route picked, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(HasValue))]
    public partial string? Value { get; private set; }

    /// <summary>Gets what the button says: the route, else that none is picked.</summary>
    public string Text => Value is { } id ? NameOf(id) : Editor.Localizer["RouteChoose"];

    /// <summary>Gets whether a route is picked.</summary>
    public bool HasValue => Value is not null;

    /// <summary>Gets whether a route can be picked: the editor shows in a window and the project is known.</summary>
    public bool CanPick => Editor.Dialogs is not null && Editor.Library is not null;

    /// <inheritdoc/>
    ICommand IPickingField.PickCommand => PickCommand;

    /// <summary>Initializes a new instance of the <see cref="RouteFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public RouteFieldViewModel(SettingDefinition definition, SettingsEditor editor)
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
        var choices = library.OfType<RouteAsset>()
            .OrderBy(route => route.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(route => (route.Id, route.Name, localizer.Plural("RoutePickerMaps", route.Anchors.Length), route.Id == Value))
            .ToList();

        dialogs.Show(new RoutePickerViewModel(dialogs, localizer["RoutePickerTitle"], localizer["RoutePickerNone"], localizer["RouteClear"], localizer["Cancel"], choices, Value is not null, Set));
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
        return Editor.Library?.Find<RouteAsset>(id) is { } route ? route.Name : Editor.Localizer.Format("RouteGone", id);
    }

    private void Set(string? route)
    {
        Value = route;
        Write(route ?? "");
    }
}
