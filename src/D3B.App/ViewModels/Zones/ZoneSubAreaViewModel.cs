using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Zones;

/// <summary>A sub-area a zone holds whole, in the list that takes it out (D394).</summary>
internal sealed partial class ZoneSubAreaViewModel
{
    private readonly Action<int> _remove;

    /// <summary>Gets the sub-area's id.</summary>
    public int Id { get; }

    /// <summary>Gets its name in the game's language.</summary>
    public string Name { get; }

    /// <summary>Gets how many maps it has, said.</summary>
    public string Detail { get; }

    /// <summary>Initializes a new instance of the <see cref="ZoneSubAreaViewModel"/> class.</summary>
    /// <param name="id">The sub-area's id.</param>
    /// <param name="name">Its name.</param>
    /// <param name="detail">How many maps it has, said.</param>
    /// <param name="remove">What taking it out does, given its id.</param>
    public ZoneSubAreaViewModel(int id, string name, string detail, Action<int> remove)
    {
        Id = id;
        Name = name;
        Detail = detail;
        _remove = remove;
    }

    [RelayCommand]
    private void Remove()
    {
        _remove(Id);
    }
}
