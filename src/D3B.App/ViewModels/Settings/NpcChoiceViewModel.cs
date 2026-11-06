using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>An NPC the picker offers: one the bots saw on the map (D298, D384).</summary>
internal sealed partial class NpcChoiceViewModel
{
    private readonly Action<int> _choose;

    /// <summary>Gets the NPC's id in the client's tables.</summary>
    public int Npc { get; }

    /// <summary>Gets what the picker calls it: the game sends no name (D383).</summary>
    public string Name { get; }

    /// <summary>Gets where it stands and what is known of it.</summary>
    public string Detail { get; }

    /// <summary>Gets whether it is the NPC picked until now.</summary>
    public bool IsSelected { get; }

    /// <summary>Initializes a new instance of the <see cref="NpcChoiceViewModel"/> class.</summary>
    /// <param name="npc">The NPC's id.</param>
    /// <param name="name">What the picker calls it.</param>
    /// <param name="detail">Where it stands and what is known of it.</param>
    /// <param name="isSelected">Whether it is picked until now.</param>
    /// <param name="choose">What choosing it does, given its id.</param>
    public NpcChoiceViewModel(int npc, string name, string detail, bool isSelected, Action<int> choose)
    {
        Npc = npc;
        Name = name;
        Detail = detail;
        IsSelected = isSelected;
        _choose = choose;
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(Npc);
    }
}
