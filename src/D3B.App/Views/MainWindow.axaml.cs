using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>The app's window: where the bots' host stands, and its bots, dark unless the theme is switched, from its title bar or the settings page.</summary>
internal sealed partial class MainWindow : ShadUI.Window
{
    private SettingsViewModel? _settings;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        ShowThemeIcon();
        ActualThemeVariantChanged += (_, _) => ShowThemeIcon();
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_settings is not null)
            _settings.PropertyChanged -= OnSettingsChanged;

        _settings = (DataContext as MainViewModel)?.Settings;

        if (_settings is not null)
            _settings.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The title bar's button and the settings page both switch it; the window draws what the page says.
        if (e.PropertyName == nameof(SettingsViewModel.IsDark) && sender is SettingsViewModel settings && Avalonia.Application.Current is { } application)
            application.RequestedThemeVariant = settings.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
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
        var dark = ActualThemeVariant != ThemeVariant.Light;

        ThemeIcon.Data = this.FindResource(dark ? "IconSun" : "IconMoon") as Geometry;
    }
}
