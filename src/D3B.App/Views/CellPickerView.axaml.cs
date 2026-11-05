using Avalonia.Controls;
using D3B.App.ViewModels.Settings;

namespace D3B.App.Views;

/// <summary>A map's cells in a dialog, one of which is picked by a press (D121, D377).</summary>
internal sealed partial class CellPickerView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="CellPickerView"/> class.</summary>
    public CellPickerView()
    {
        InitializeComponent();
        Cells.CellPressed += (_, cell) => (DataContext as CellPickerViewModel)?.Choose(cell);
        Cells.CellHovered += (_, cell) => (DataContext as CellPickerViewModel)?.Point(cell);
    }
}
