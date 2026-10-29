using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A value of a list setting, at its place, which may be moved when the list's order matters, or removed.</summary>
internal sealed partial class ListItemViewModel
{
    private readonly ListFieldViewModel _list;

    /// <summary>Gets the value.</summary>
    public OptionViewModel Option { get; }

    /// <summary>Gets its place, from 0.</summary>
    public int Index { get; }

    /// <summary>Gets its place as the list shows it, from 1.</summary>
    public string Position { get; }

    /// <summary>Gets whether the list's order matters.</summary>
    public bool IsOrdered { get; }

    /// <summary>Gets whether it may move up.</summary>
    public bool CanMoveUp { get; }

    /// <summary>Gets whether it may move down.</summary>
    public bool CanMoveDown { get; }

    /// <summary>Initializes a new instance of the <see cref="ListItemViewModel"/> class.</summary>
    /// <param name="list">The list it belongs to.</param>
    /// <param name="option">The value.</param>
    /// <param name="index">Its place.</param>
    /// <param name="count">How many values the list holds.</param>
    public ListItemViewModel(ListFieldViewModel list, OptionViewModel option, int index, int count)
    {
        _list = list;
        Option = option;
        Index = index;
        Position = (index + 1).ToString(list.Culture);
        IsOrdered = list.Definition.IsOrdered;
        CanMoveUp = IsOrdered && index > 0;
        CanMoveDown = IsOrdered && index < count - 1;
    }

    [RelayCommand]
    private void MoveUp()
    {
        _list.Move(Index, -1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        _list.Move(Index, 1);
    }

    [RelayCommand]
    private void Remove()
    {
        _list.Remove(Index);
    }
}
