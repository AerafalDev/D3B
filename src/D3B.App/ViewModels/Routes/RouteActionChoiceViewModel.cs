using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Routes;

/// <summary>An action the menu offers to add to a step (D377).</summary>
internal sealed partial class RouteActionChoiceViewModel
{
    private readonly Action<string> _add;

    /// <summary>Gets the action's node id.</summary>
    public string Node { get; }

    /// <summary>Gets what the menu says of it.</summary>
    public string Name { get; }

    /// <summary>Initializes a new instance of the <see cref="RouteActionChoiceViewModel"/> class.</summary>
    /// <param name="node">The action's node id.</param>
    /// <param name="name">What the menu says of it.</param>
    /// <param name="add">What adding it to the step does, given the node id.</param>
    public RouteActionChoiceViewModel(string node, string name, Action<string> add)
    {
        Node = node;
        Name = name;
        _add = add;
    }

    [RelayCommand]
    private void Add()
    {
        _add(Node);
    }
}
