using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>The home page, the editor's first tab: the characters' cards, what to make, the assets changed last, and where the host stands while it does not run (D320, D406, D514).</summary>
internal sealed partial class HomeView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="HomeView"/> class.</summary>
    public HomeView()
    {
        InitializeComponent();
    }

    private async void OnCopyFailureClick(object? sender, RoutedEventArgs e)
    {
        // What to run to fix it, often: a command the owner pastes in a terminal.
        if (DataContext is MainViewModel { HostFailure: { } failure } && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(failure);
    }
}
