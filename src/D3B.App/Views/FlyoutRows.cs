using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;

namespace D3B.App.Views;

/// <summary>Closes the menu a row of a toolbar's drop-down was chosen in, as a menu's item does (D327).</summary>
internal static class FlyoutRows
{
    /// <summary>Closes the drop-down holding a row.</summary>
    /// <param name="sender">The row clicked.</param>
    public static void Close(object? sender)
    {
        (sender as Control)?.FindLogicalAncestorOfType<Popup>()?.Close();
    }
}
