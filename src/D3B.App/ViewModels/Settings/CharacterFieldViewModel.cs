using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Characters;
using D3B.Bot.Settings;
using D3B.Storage.Accounts;

namespace D3B.App.ViewModels.Settings;

/// <summary>A character of the accounts page, picked among the characters D3B knows in a dialog, never typed, such as a follower's master (D436).</summary>
/// <remarks>The dialog is the route's, with the characters' words: each its name, its level and its account's nickname. A character D3B no longer knows is a problem that blocks the profile.</remarks>
internal sealed partial class CharacterFieldViewModel : SettingFieldViewModel, IPickingField
{
    /// <summary>Gets the key of the character picked, as the setting holds it, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(HasValue))]
    public partial string? Value { get; private set; }

    /// <summary>Gets what the button says: the character, else that none is picked.</summary>
    public string Text => Value is { } id ? NameOf(id) : Editor.Localizer["CharacterChoose"];

    /// <summary>Gets what is said under the field: what the character does for the activity.</summary>
    public string? Detail => Help;

    /// <summary>Gets whether a character is picked.</summary>
    public bool HasValue => Value is not null;

    /// <summary>Gets whether a character can be picked: the editor shows in a window and D3B's accounts are known.</summary>
    public bool CanPick => Editor.Dialogs is not null && Accounts is not null;

    /// <inheritdoc/>
    public override bool HasInputProblem => ProblemOf() is not null;

    /// <inheritdoc/>
    ICommand IPickingField.PickCommand => PickCommand;

    private AccountBook? Accounts => Editor.Accounts;

    /// <summary>Initializes a new instance of the <see cref="CharacterFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public CharacterFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = Read());
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Detail));
    }

    /// <summary>Picks a character, or none, as the dialog does.</summary>
    /// <param name="character">The character's key, as the setting holds it, or <see langword="null"/> for none.</param>
    public void Choose(string? character)
    {
        Value = character;
        Write(character ?? "");
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        Value = Read();
        OnPropertyChanged(nameof(CanPick));
        OnPropertyChanged(nameof(Text));
    }

    /// <inheritdoc/>
    protected override string? InputProblem()
    {
        return ProblemOf();
    }

    [RelayCommand]
    private void Pick()
    {
        if (Editor.Dialogs is not { } dialogs || Accounts is not { } accounts)
            return;

        var localizer = Editor.Localizer;
        var nicknames = accounts.Accounts.ToDictionary(account => account.Id, account => account.Nickname);
        var choices = accounts.Characters
            .OrderBy(character => character.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(character =>
            {
                var id = character.Key.ToId();
                var account = nicknames.GetValueOrDefault(character.AccountId, "");

                return (id, character.Name, character.Level is { } level ? localizer.Format("CharacterPickerDetail", level, account) : account, id == Value);
            })
            .ToList();

        dialogs.Show(new RoutePickerViewModel(dialogs, localizer["CharacterPickerTitle"], localizer["CharacterPickerNone"], localizer["CharacterClear"], localizer["Cancel"], choices, Value is not null, Choose));
    }

    [RelayCommand]
    private void Clear()
    {
        Choose(null);
    }

    private string? Read()
    {
        return Definition.GetValue(Draft) is string { Length: > 0 } id ? id : null;
    }

    private KnownCharacter? Find(string id)
    {
        return CharacterKey.TryParseId(id, out var key) ? Accounts?.Find(key) : null;
    }

    private string? ProblemOf()
    {
        // An editor that cannot see D3B's accounts cannot tell a character is gone.
        if (Value is not { } id || Accounts is null)
            return null;

        return Find(id) is null ? Editor.Localizer["CharacterNotKnown"] : null;
    }

    private string NameOf(string id)
    {
        return Find(id) is { } character ? character.Name : Editor.Localizer.Format("CharacterGone", id);
    }
}
