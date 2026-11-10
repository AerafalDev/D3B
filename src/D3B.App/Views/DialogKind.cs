using Avalonia.Controls;
using D3B.App.ViewModels;
using ShadUI;

namespace D3B.App.Views;

/// <summary>A kind of dialog the window shows: the view model it is for, its view and its width.</summary>
/// <remarks>
/// One table of these drives both the registration of the views and the showing of each dialog, so that a dialog
/// registered is a dialog shown: a view model with no row here is never drawn.
/// </remarks>
internal sealed class DialogKind
{
    private readonly Action<DialogManager> _register;
    private readonly Action<DialogManager, object> _show;

    /// <summary>Gets the type of the view model this kind shows.</summary>
    public Type ViewModel { get; }

    private DialogKind(Type viewModel, Action<DialogManager> register, Action<DialogManager, object> show)
    {
        ViewModel = viewModel;
        _register = register;
        _show = show;
    }

    /// <summary>Describes a kind of dialog.</summary>
    /// <typeparam name="TView">The dialog's view.</typeparam>
    /// <typeparam name="TViewModel">The dialog's view model.</typeparam>
    /// <param name="maxWidth">The widest the dialog grows.</param>
    /// <param name="minWidth">The narrowest it is, or <see langword="null"/> to fit its content.</param>
    /// <returns>The kind.</returns>
    public static DialogKind Of<TView, TViewModel>(double maxWidth, double? minWidth = null)
        where TView : Control
        where TViewModel : class, IDialogViewModel
    {
        return new DialogKind(
            typeof(TViewModel),
            manager => manager.Register<TView, TViewModel>(),
            (manager, viewModel) =>
            {
                var builder = manager.CreateDialog((TViewModel)viewModel);

                if (minWidth is { } min)
                    builder = builder.WithMinWidth(min);

                builder.WithMaxWidth(maxWidth).Show();
            });
    }

    /// <summary>Registers the dialog's view with the manager.</summary>
    /// <param name="manager">The window's dialog manager.</param>
    public void Register(DialogManager manager)
    {
        _register(manager);
    }

    /// <summary>Shows a dialog of this kind.</summary>
    /// <param name="manager">The window's dialog manager.</param>
    /// <param name="viewModel">The dialog's view model.</param>
    public void Show(DialogManager manager, object viewModel)
    {
        _show(manager, viewModel);
    }
}
