using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Routes;

/// <summary>A world the layer can show, in the menu of the map's top left (D299).</summary>
/// <remarks>It holds its own command: the menu is a popup, outside the tab's visual tree, where nothing can bind to the tab's model.</remarks>
internal sealed partial class WorldChoiceViewModel
{
    private readonly Action<int> _choose;

    /// <summary>Gets the world's id.</summary>
    public int Id { get; }

    /// <summary>Gets its name in the game's language.</summary>
    public string Name { get; }

    /// <summary>Gets how many maps it holds, its insides included.</summary>
    public int Count { get; }

    /// <summary>Gets the count, said.</summary>
    public string Detail { get; }

    /// <summary>Gets whether it is the layer shown.</summary>
    public bool IsChosen { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldChoiceViewModel"/> class.</summary>
    /// <param name="id">The world's id.</param>
    /// <param name="name">Its name.</param>
    /// <param name="count">How many maps it holds.</param>
    /// <param name="detail">The count, said.</param>
    /// <param name="isChosen">Whether it is the layer shown.</param>
    /// <param name="choose">What choosing it does, given its id.</param>
    public WorldChoiceViewModel(int id, string name, int count, string detail, bool isChosen, Action<int> choose)
    {
        Id = id;
        Name = name;
        Count = count;
        Detail = detail;
        IsChosen = isChosen;
        _choose = choose;
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(Id);
    }
}
