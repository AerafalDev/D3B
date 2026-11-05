using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Data.Maps;

namespace D3B.App.ViewModels.Settings;

/// <summary>The dialog that picks a cell on a map's own cells (D121, D377).</summary>
internal sealed partial class CellPickerViewModel : ObservableObject
{
    private readonly Dialogs _dialogs;
    private readonly Action<int?> _picked;

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets what the dialog says of how to pick.</summary>
    public string Hint { get; }

    /// <summary>Gets the text of the button that takes the cell back.</summary>
    public string ClearText { get; }

    /// <summary>Gets the text of the button that closes the dialog.</summary>
    public string CancelText { get; }

    /// <summary>Gets the map whose cells are drawn.</summary>
    public MapData Map { get; }

    /// <summary>Gets the cell under the pointer, in words, or an empty text.</summary>
    [ObservableProperty]
    public partial string Pointed { get; private set; } = "";

    /// <summary>Gets the cell picked now, or <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial int? Selected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CellPickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="map">The map.</param>
    /// <param name="selected">The cell picked until now.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="hint">How to pick.</param>
    /// <param name="clearText">The text of the button that takes the cell back.</param>
    /// <param name="cancelText">The text of the button that closes the dialog.</param>
    /// <param name="picked">What the cell picked does, or <see langword="null"/> for none.</param>
    public CellPickerViewModel(Dialogs dialogs, MapData map, int? selected, string title, string hint, string clearText, string cancelText, Action<int?> picked)
    {
        _dialogs = dialogs;
        Map = map;
        Selected = selected;
        Title = title;
        Hint = hint;
        ClearText = clearText;
        CancelText = cancelText;
        _picked = picked;
    }

    /// <summary>Picks a cell and closes the dialog.</summary>
    /// <param name="cell">The cell.</param>
    public void Choose(int cell)
    {
        _picked(cell);
        _dialogs.Close(this);
    }

    /// <summary>Says which cell the pointer is on.</summary>
    /// <param name="cell">The cell, or -1 off the map.</param>
    public void Point(int cell)
    {
        Pointed = cell < 0 ? "" : cell.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private void Clear()
    {
        _picked(null);
        _dialogs.Close(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }
}
