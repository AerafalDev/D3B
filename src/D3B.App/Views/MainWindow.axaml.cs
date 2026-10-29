using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>The app's window: where the bots' host stands, and its bots, dark unless the theme is switched.</summary>
internal sealed partial class MainWindow : ShadUI.Window
{
    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        ShowThemeIcon();
    }

    private void OnThemeClick(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current is not { } application)
            return;

        application.RequestedThemeVariant = application.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        ShowThemeIcon();
        (DataContext as MainViewModel)?.RememberTheme(application.RequestedThemeVariant == ThemeVariant.Dark);
    }

    private async void OnCopyFailureClick(object? sender, RoutedEventArgs e)
    {
        // What to run to fix it, often: a command the owner pastes in a terminal.
        if (DataContext is MainViewModel { HostFailure: { } failure } && Clipboard is { } clipboard)
            await clipboard.SetTextAsync(failure);
    }

    private void ShowThemeIcon()
    {
        // The icon shows the theme a click switches to.
        var dark = Avalonia.Application.Current?.ActualThemeVariant != ThemeVariant.Light;

        ThemeIcon.Data = this.FindResource(dark ? "IconSun" : "IconMoon") as Geometry;
    }
}
