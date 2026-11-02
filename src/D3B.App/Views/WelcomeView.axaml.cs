using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>The editor's welcome: D3B's logo and version, the first shortcuts, and where the host stands while it does not run (D320).</summary>
internal sealed partial class WelcomeView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="WelcomeView"/> class.</summary>
    public WelcomeView()
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
