using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Sequences;

/// <summary>A condition the "until" of a repeat can wait on, in the menu that chooses it (D420).</summary>
internal sealed partial class RepeatConditionChoiceViewModel
{
    private readonly Action<string> _choose;

    /// <summary>Gets the condition's node id.</summary>
    public string NodeId { get; }

    /// <summary>Gets the condition as the menu says it: its sentence, with a gap where a value goes.</summary>
    public string Name { get; }

    /// <summary>Initializes a new instance of the <see cref="RepeatConditionChoiceViewModel"/> class.</summary>
    /// <param name="nodeId">The condition's node id.</param>
    /// <param name="name">What the menu says of it.</param>
    /// <param name="choose">What choosing it does, given the node id.</param>
    public RepeatConditionChoiceViewModel(string nodeId, string name, Action<string> choose)
    {
        NodeId = nodeId;
        Name = name;
        _choose = choose;
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(NodeId);
    }
}
