using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Npcs;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>The replies given to the NPC chosen, picked one question after another among those the bots saw offered, never typed (D298, D384).</summary>
internal sealed partial class NpcRepliesFieldViewModel : SettingFieldViewModel, IPickingField
{
    /// <summary>Gets the replies, in the order of the questions.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(HasValue))]
    public partial IReadOnlyList<int> Value { get; private set; } = [];

    /// <summary>Gets what the button says: the replies, else that there is none.</summary>
    public string Text => Value.Count == 0 ? Editor.Localizer["NpcRepliesNone"] : string.Join(", ", Value);

    /// <summary>Gets whether a reply is picked.</summary>
    public bool HasValue => Value.Count > 0;

    /// <summary>Gets whether the replies can be picked: the editor shows in a window, and the map, the NPC and what the bots learned of it are known.</summary>
    public bool CanPick => Editor.Dialogs is not null && Editor.Map is not null && Editor.Npcs is not null && Npc is not null;

    /// <inheritdoc/>
    ICommand IPickingField.PickCommand => PickCommand;

    private int? Npc => Editor.FieldOf(NpcKey) is NpcFieldViewModel { Value: { } npc } ? npc : null;

    private static string NpcKey => "Npc";

    /// <summary>Initializes a new instance of the <see cref="NpcRepliesFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public NpcRepliesFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = [.. definition.GetValues(Draft).Cast<int>()]);
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        OnPropertyChanged(nameof(Text));
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        Value = [.. Definition.GetValues(Draft).Cast<int>()];
        OnPropertyChanged(nameof(CanPick));
    }

    [RelayCommand]
    private void Pick()
    {
        if (Editor.Dialogs is not { } dialogs || Editor.Map is not { } map || Editor.Npcs is not { } npcs || Npc is not { } npc)
            return;

        // An NPC the bots have not seen on this map has no dialog to follow: the picker then says so.
        var known = npcs.OnMap(map.Id).FirstOrDefault(candidate => candidate.Npc.NpcId == npc)
            ?? new KnownNpc(new KnownMapNpc(map.Id, npc, 0, null, null, 0, default, default), [], []);

        dialogs.Show(new RepliesPickerViewModel(dialogs, known, Value, Editor.Localizer, Editor.Place, Set));
    }

    /// <summary>Takes the replies back, as another NPC is chosen.</summary>
    public void Forget()
    {
        if (Value.Count > 0)
            Set([]);
    }

    [RelayCommand]
    private void Clear()
    {
        Set([]);
    }

    private void Set(IReadOnlyList<int> replies)
    {
        Value = [.. replies];
        Write(Value);
    }
}
