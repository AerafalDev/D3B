using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Routes;

/// <summary>The dialog that offers the actions a step may be given, one press adding it (D381).</summary>
internal sealed partial class RouteActionPickerViewModel
{
    private readonly Dialogs _dialogs;

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets the text of the button that closes the dialog.</summary>
    public string CancelText { get; }

    /// <summary>Gets the actions offered.</summary>
    public IReadOnlyList<RouteActionChoiceViewModel> Actions { get; }

    /// <summary>Initializes a new instance of the <see cref="RouteActionPickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="cancelText">The text of the button that closes the dialog.</param>
    /// <param name="actions">The actions offered.</param>
    public RouteActionPickerViewModel(Dialogs dialogs, string title, string cancelText, IReadOnlyList<RouteActionChoiceViewModel> actions)
    {
        _dialogs = dialogs;
        Title = title;
        CancelText = cancelText;
        Actions = actions;
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }
}
