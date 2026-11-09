using CommunityToolkit.Mvvm.ComponentModel;

namespace D3B.App.ViewModels.World;

/// <summary>A category of the world map's hints, as the View menu offers it: a tick that shows or hides its pictures (D414).</summary>
internal sealed partial class HintCategoryViewModel : ObservableObject
{
    private readonly Action? _changed;

    /// <summary>Gets the client's identifier of the category.</summary>
    public int Id { get; }

    /// <summary>Gets the category's name, as the game's own texts say it in the app's language.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Gets how many hints of the category stand on the world shown.</summary>
    [ObservableProperty]
    public partial int Count { get; set; }

    /// <summary>Gets or sets whether the map draws the category's hints.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; } = true;

    /// <summary>Initializes a new instance of the <see cref="HintCategoryViewModel"/> class.</summary>
    /// <param name="id">The category.</param>
    /// <param name="name">Its name.</param>
    /// <param name="isOn">Whether its hints are drawn.</param>
    /// <param name="changed">Called when the tick changes.</param>
    public HintCategoryViewModel(int id, string name, bool isOn, Action changed)
    {
        Id = id;
        Name = name;
        IsOn = isOn;

        // Set last: ticking the category as it is made says nothing to anyone.
        _changed = changed;
    }

    partial void OnIsOnChanged(bool value)
    {
        _changed?.Invoke();
    }
}
