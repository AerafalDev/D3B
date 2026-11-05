using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>An NPC of the map a node plays on, picked among those the bots learned there, never typed (D298, D384).</summary>
internal sealed partial class NpcFieldViewModel : SettingFieldViewModel, IPickingField
{
    /// <summary>Gets the NPC picked, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(HasValue))]
    public partial int? Value { get; private set; }

    /// <summary>Gets what the button says: the NPC, else that none is picked.</summary>
    public string Text => Value is { } npc ? NameOf(npc) : Editor.Localizer["NpcChoose"];

    /// <summary>Gets whether an NPC is picked.</summary>
    public bool HasValue => Value is not null;

    /// <summary>Gets whether an NPC can be picked: the editor shows in a window, and the map and what the bots learned are known.</summary>
    public bool CanPick => Editor.Dialogs is not null && Editor.Map is not null && Editor.Npcs is not null;

    /// <inheritdoc/>
    ICommand IPickingField.PickCommand => PickCommand;

    /// <summary>Initializes a new instance of the <see cref="NpcFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public NpcFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = definition.GetValue(Draft) as int?);
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
        Value = Definition.GetValue(Draft) as int?;
        OnPropertyChanged(nameof(CanPick));
    }

    [RelayCommand]
    private void Pick()
    {
        if (Editor.Dialogs is not { } dialogs || Editor.Map is not { } map || Editor.Npcs is not { } npcs)
            return;

        var localizer = Editor.Localizer;
        var choices = npcs.OnMap(map.Id)
            .Select(known => (
                known.Npc.NpcId,
                NameOf(known.Npc.NpcId),
                localizer.Format("NpcDetail", known.Npc.Cell is { } cell ? cell.ToString(localizer.Culture) : "?", localizer.Plural("NpcSeen", known.Npc.Seen), localizer.Plural("NpcQuestions", known.Questions.Length)),
                known.Npc.NpcId == Value))
            .ToList();

        dialogs.Show(new NpcPickerViewModel(dialogs, localizer["NpcPickerTitle"], localizer["NpcPickerNone"], localizer["NpcClear"], localizer["Cancel"], choices, Value is not null, Set));
    }

    [RelayCommand]
    private void Clear()
    {
        Set(null);
    }

    private string NameOf(int npc)
    {
        return Editor.NpcNames?.Npc(npc) ?? Editor.Localizer.Format("NpcNumber", npc);
    }

    private void Set(int? npc)
    {
        var other = Value != npc;

        Value = npc;
        Write(npc);

        // Replies answer one NPC's questions: another NPC, another dialog.
        if (other && Editor.FieldOf("Replies") is NpcRepliesFieldViewModel replies)
            replies.Forget();
    }
}
