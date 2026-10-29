using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>One of an enumeration's values, as a list to choose from.</summary>
internal sealed partial class ChoiceFieldViewModel : SettingFieldViewModel
{
    /// <summary>Gets the values the choice offers, named in the app's language.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OptionViewModel> Options { get; private set; } = [];

    /// <summary>Gets or sets the value chosen.</summary>
    [ObservableProperty]
    public partial OptionViewModel? Selected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="ChoiceFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public ChoiceFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Read();
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        Read();
    }

    private void Read()
    {
        var culture = Editor.Localizer.Culture;
        var value = Definition.GetValue(Draft);

        Quiet(() =>
        {
            Options = [.. Definition.Options.Select(option => new OptionViewModel(option.Value, option.Text.In(culture)))];
            Selected = Options.FirstOrDefault(option => option.Value.Equals(value));
        });
    }

    partial void OnSelectedChanged(OptionViewModel? value)
    {
        if (value is not null)
            Write(value.Value);
    }
}
