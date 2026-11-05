using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.ViewModels.Settings;

namespace D3B.App.ViewModels.Routes;

/// <summary>A piece of an action's sentence: some words, or one of its values as a token that edits it where it stands (D380).</summary>
internal sealed partial class ActionPartViewModel : ObservableObject
{
    /// <summary>Gets the words, or the value as it reads in the sentence.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; }

    /// <summary>Gets whether something is wrong with the value, which the token says.</summary>
    [ObservableProperty]
    public partial bool HasProblem { get; private set; }

    /// <summary>Gets the field the token edits, or <see langword="null"/> for words.</summary>
    public SettingFieldViewModel? Field { get; }

    /// <summary>Gets whether the part is a value.</summary>
    public bool IsToken => Field is not null;

    /// <summary>Gets whether the part is words.</summary>
    public bool IsWords => Field is null;

    /// <summary>Gets whether the token is picked in a dialog of its own, which it opens at once: a cell, an NPC or its replies.</summary>
    public bool IsDialog => Field is IPickingField;

    /// <summary>Gets whether the token opens a popover of its field: every value but those picked in a dialog.</summary>
    public bool IsPopup => Field is not null and not IPickingField;

    /// <summary>Gets what opens the dialog, for a token picked in one.</summary>
    public ICommand? PickCommand => (Field as IPickingField)?.PickCommand;

    /// <summary>Initializes a new instance of the <see cref="ActionPartViewModel"/> class.</summary>
    /// <param name="text">The words, or the value's text.</param>
    /// <param name="field">The field the part edits, or <see langword="null"/> for words.</param>
    public ActionPartViewModel(string text, SettingFieldViewModel? field)
    {
        Text = text;
        Field = field;
        HasProblem = field is { HasProblem: true };
    }

    /// <summary>Shows the value as it is now.</summary>
    /// <param name="text">The value's text.</param>
    public void Show(string text)
    {
        Text = text;
        HasProblem = Field is { HasProblem: true };
    }
}
