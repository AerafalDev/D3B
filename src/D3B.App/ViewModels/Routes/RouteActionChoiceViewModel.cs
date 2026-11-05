using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Routes;

/// <summary>An action the picker offers to add to a step (D377, D381).</summary>
internal sealed partial class RouteActionChoiceViewModel
{
    private readonly Action<string> _add;

    /// <summary>Gets the action's node id.</summary>
    public string Node { get; }

    /// <summary>Gets what the picker says of it.</summary>
    public string Name { get; }

    /// <summary>Gets what the action does, in a line.</summary>
    public string Help { get; }

    /// <summary>Initializes a new instance of the <see cref="RouteActionChoiceViewModel"/> class.</summary>
    /// <param name="node">The action's node id.</param>
    /// <param name="name">What the picker says of it.</param>
    /// <param name="help">What it does.</param>
    /// <param name="add">What adding it to the step does, given the node id.</param>
    public RouteActionChoiceViewModel(string node, string name, string help, Action<string> add)
    {
        Node = node;
        Name = name;
        Help = help;
        _add = add;
    }

    [RelayCommand]
    private void Add()
    {
        _add(Node);
    }
}
