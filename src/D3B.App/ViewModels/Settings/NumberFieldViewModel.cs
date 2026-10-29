using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A whole number between its bounds, as a box with steps.</summary>
internal sealed partial class NumberFieldViewModel : SettingFieldViewModel
{
    private bool _empty;

    /// <summary>Gets the lowest value.</summary>
    public decimal Minimum => Definition.Minimum ?? int.MinValue;

    /// <summary>Gets the highest value.</summary>
    public decimal Maximum => Definition.Maximum ?? int.MaxValue;

    /// <summary>Gets or sets the number, or <see langword="null"/> once the box is emptied.</summary>
    [ObservableProperty]
    public partial decimal? Value { get; set; }

    /// <summary>Gets what the box takes, from its lowest value to its highest.</summary>
    [ObservableProperty]
    public partial string Range { get; private set; } = "";

    /// <inheritdoc/>
    public override bool HasInputProblem => _empty;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public NumberFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Range = editor.Localizer.Format("NumberRange", Minimum, Maximum);
        Quiet(() => Value = (int)definition.GetValue(Draft)!);
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        Range = Editor.Localizer.Format("NumberRange", Minimum, Maximum);
    }

    /// <inheritdoc/>
    protected override string? InputProblem()
    {
        return _empty ? Editor.Localizer["SettingRequired"] : null;
    }

    partial void OnValueChanged(decimal? value)
    {
        _empty = value is null;

        if (value is { } number)
            Write((int)Math.Round(Math.Clamp(number, int.MinValue, int.MaxValue)));
        else
            Touched();
    }
}
