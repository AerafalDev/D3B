using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Home;

namespace D3B.App.Views;

/// <summary>The home page, the editor's first tab: quick actions, the characters, the assets changed last, and where the host stands while it does not run (D320, D406).</summary>
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

    private void OnCharacterDoubleTapped(object? sender, TappedEventArgs e)
    {
        // A click chooses the character, a double click opens its tab, as the Outliner's rows do.
        if ((sender as Control)?.DataContext is HomeCharacterViewModel character)
            character.OpenCommand.Execute(null);
    }
}
