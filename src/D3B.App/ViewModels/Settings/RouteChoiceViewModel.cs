using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A route the picker offers: one of the project's (D298, D386).</summary>
internal sealed partial class RouteChoiceViewModel
{
    private readonly Action<string> _choose;

    /// <summary>Gets the route's id.</summary>
    public string Route { get; }

    /// <summary>Gets the route's name.</summary>
    public string Name { get; }

    /// <summary>Gets what is said of it: how many maps it goes through.</summary>
    public string Detail { get; }

    /// <summary>Gets whether it is the route picked until now.</summary>
    public bool IsSelected { get; }

    /// <summary>Initializes a new instance of the <see cref="RouteChoiceViewModel"/> class.</summary>
    /// <param name="route">The route's id.</param>
    /// <param name="name">The route's name.</param>
    /// <param name="detail">What is said of it.</param>
    /// <param name="isSelected">Whether it is picked until now.</param>
    /// <param name="choose">What choosing it does, given its id.</param>
    public RouteChoiceViewModel(string route, string name, string detail, bool isSelected, Action<string> choose)
    {
        Route = route;
        Name = name;
        Detail = detail;
        IsSelected = isSelected;
        _choose = choose;
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(Route);
    }
}
