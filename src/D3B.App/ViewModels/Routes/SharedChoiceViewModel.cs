using Avalonia;
using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Routes;

/// <summary>The choice among the maps that share coordinates, which a click on their mark or a typed pair of coordinates asks for (D299).</summary>
internal sealed partial class SharedChoiceViewModel
{
    private readonly Action<MapChoiceViewModel> _choose;
    private readonly Action _close;

    /// <summary>Gets the title: how many maps, and where.</summary>
    public string Title { get; }

    /// <summary>Gets the maps to choose among.</summary>
    public IReadOnlyList<MapChoiceViewModel> Items { get; }

    /// <summary>Gets where the choice opens in the map's view, or <see langword="null"/> for under the toolbar.</summary>
    public Point? At { get; }

    /// <summary>Initializes a new instance of the <see cref="SharedChoiceViewModel"/> class.</summary>
    /// <param name="title">How many maps, and where.</param>
    /// <param name="items">The maps.</param>
    /// <param name="at">The point of the view it opens at, or <see langword="null"/>.</param>
    /// <param name="choose">What choosing a map does.</param>
    /// <param name="close">What closing the choice does.</param>
    public SharedChoiceViewModel(string title, IReadOnlyList<MapChoiceViewModel> items, Point? at, Action<MapChoiceViewModel> choose, Action close)
    {
        Title = title;
        Items = items;
        At = at;
        _choose = choose;
        _close = close;
    }

    [RelayCommand]
    private void Choose(MapChoiceViewModel? map)
    {
        if (map is not null)
            _choose(map);
    }

    [RelayCommand]
    private void Close()
    {
        _close();
    }
}
