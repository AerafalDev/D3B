using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>The settings' tab: its sections in a column, the one chosen beside, a row per setting (D151, D329).</summary>
internal sealed partial class SettingsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="SettingsView"/> class.</summary>
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnCopyPathClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string { Length: > 0 } path } && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(path);
            (DataContext as SettingsViewModel)?.Copied();
        }
    }
}
