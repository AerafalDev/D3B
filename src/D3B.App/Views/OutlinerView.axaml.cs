using Avalonia.Controls;
using Avalonia.Input;
using D3B.App.ViewModels.World;

namespace D3B.App.Views;

/// <summary>The Outliner: a click selects what the row names, a double click opens a character's tab, the right click on a character opens its menu (D397).</summary>
internal sealed partial class OutlinerView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="OutlinerView"/> class.</summary>
    public OutlinerView()
    {
        InitializeComponent();
    }

    private static OutlinerRowViewModel? RowOf(object? sender)
    {
        return (sender as Control)?.DataContext as OutlinerRowViewModel;
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        Choose(sender);
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is OutlinerViewModel panel && RowOf(sender) is { } row)
            panel.Open(row);
    }

    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not OutlinerViewModel panel || RowOf(sender) is not { } row)
            return;

        if (e.Key is Key.Enter)
        {
            panel.Open(row);
            e.Handled = true;
        }
        else if (e.Key is Key.Space)
        {
            Choose(sender);
            e.Handled = true;
        }
        else if (e.Key is Key.Right or Key.Left && row.CanExpand && row.IsExpanded == (e.Key is Key.Left))
        {
            panel.ToggleExpanded(row);
            e.Handled = true;
        }
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not OutlinerViewModel panel || RowOf(sender) is not { Kind: OutlinerRowKind.Character } row || sender is not Control target || CharacterKeys.Parse(row.Id) is not { } key)
            return;

        // The characters' panel is the model of the menu: it chooses the character, as the old cards did.
        var characters = panel.Characters;

        Choose(sender);
        characters.Choose(key);
        e.Handled = true;
        CharacterMenu.Open(target, characters);
    }

    private void Choose(object? sender)
    {
        if (DataContext is OutlinerViewModel panel && RowOf(sender) is { } row && sender is Control control)
        {
            panel.Select(row);
            control.Focus();
        }
    }
}
