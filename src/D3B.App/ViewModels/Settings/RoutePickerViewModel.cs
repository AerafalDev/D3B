using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>The dialog that picks a route among the project's: no id is typed (D298, D386).</summary>
internal sealed partial class RoutePickerViewModel
{
    private readonly Dialogs _dialogs;
    private readonly Action<string?> _picked;

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets what the dialog says when the project holds no route.</summary>
    public string NoneText { get; }

    /// <summary>Gets the text of the button that takes the route back.</summary>
    public string ClearText { get; }

    /// <summary>Gets the text of the button that closes the dialog.</summary>
    public string CancelText { get; }

    /// <summary>Gets the routes offered.</summary>
    public IReadOnlyList<RouteChoiceViewModel> Routes { get; }

    /// <summary>Gets whether the project holds no route.</summary>
    public bool IsEmpty => Routes.Count == 0;

    /// <summary>Gets whether a route is picked until now, which the dialog can take back.</summary>
    public bool CanClear { get; }

    /// <summary>Initializes a new instance of the <see cref="RoutePickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="noneText">What the dialog says when there is no route to pick.</param>
    /// <param name="clearText">The text of the button that takes the route back.</param>
    /// <param name="cancelText">The text of the button that closes the dialog.</param>
    /// <param name="routes">The routes offered, as (id, name, detail, picked until now).</param>
    /// <param name="canClear">Whether a route is picked until now.</param>
    /// <param name="picked">What the choice does: the route's id, or <see langword="null"/> when it is taken back.</param>
    public RoutePickerViewModel(Dialogs dialogs, string title, string noneText, string clearText, string cancelText, IEnumerable<(string Id, string Name, string Detail, bool IsSelected)> routes, bool canClear, Action<string?> picked)
    {
        _dialogs = dialogs;
        _picked = picked;
        Title = title;
        NoneText = noneText;
        ClearText = clearText;
        CancelText = cancelText;
        CanClear = canClear;
        Routes = [.. routes.Select(route => new RouteChoiceViewModel(route.Id, route.Name, route.Detail, route.IsSelected, Choose))];
    }

    private void Choose(string route)
    {
        _picked(route);
        _dialogs.Close(this);
    }

    [RelayCommand]
    private void Clear()
    {
        _picked(null);
        _dialogs.Close(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }
}
