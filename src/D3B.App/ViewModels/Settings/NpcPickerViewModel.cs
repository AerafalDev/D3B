using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>The dialog that picks an NPC among those the bots learned on a map: no id is typed (D298, D384).</summary>
internal sealed partial class NpcPickerViewModel
{
    private readonly Dialogs _dialogs;
    private readonly Action<int?> _picked;

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets what the dialog says when the bots saw no NPC on the map.</summary>
    public string NoneText { get; }

    /// <summary>Gets the text of the button that takes the NPC back.</summary>
    public string ClearText { get; }

    /// <summary>Gets the text of the button that closes the dialog.</summary>
    public string CancelText { get; }

    /// <summary>Gets the NPCs offered.</summary>
    public IReadOnlyList<NpcChoiceViewModel> Npcs { get; }

    /// <summary>Gets whether the bots saw no NPC on the map.</summary>
    public bool IsEmpty => Npcs.Count == 0;

    /// <summary>Gets whether an NPC is picked until now, which the dialog can take back.</summary>
    public bool CanClear { get; }

    /// <summary>Initializes a new instance of the <see cref="NpcPickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="noneText">What the dialog says when there is no NPC to pick.</param>
    /// <param name="clearText">The text of the button that takes the NPC back.</param>
    /// <param name="cancelText">The text of the button that closes the dialog.</param>
    /// <param name="npcs">The NPCs offered, as (id, name, detail, picked until now).</param>
    /// <param name="canClear">Whether an NPC is picked until now.</param>
    /// <param name="picked">What the choice does: the NPC's id, or <see langword="null"/> when it is taken back.</param>
    public NpcPickerViewModel(Dialogs dialogs, string title, string noneText, string clearText, string cancelText, IEnumerable<(int Npc, string Name, string Detail, bool IsSelected)> npcs, bool canClear, Action<int?> picked)
    {
        _dialogs = dialogs;
        _picked = picked;
        Title = title;
        NoneText = noneText;
        ClearText = clearText;
        CancelText = cancelText;
        CanClear = canClear;
        Npcs = [.. npcs.Select(npc => new NpcChoiceViewModel(npc.Npc, npc.Name, npc.Detail, npc.IsSelected, Choose))];
    }

    private void Choose(int npc)
    {
        _picked(npc);
        _dialogs.Close(this);
    }

    [RelayCommand]
    private void Clear()
    {
        _picked(null);
        _dialogs.Close(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }
}
