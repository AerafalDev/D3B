using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Behaviors;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.Properties;

/// <summary>A character's variable in the inspector: its name, a dot of its type in the wires' colours, and its editor (D313, D324).</summary>
internal sealed partial class VariableRowViewModel : ObservableObject
{
    private readonly CharacterVariables _variables;
    private readonly CharacterKey _character;
    private bool _reading;

    /// <summary>Gets the variable's name.</summary>
    public string Name { get; }

    /// <summary>Gets its type, as its value says it.</summary>
    public VariableType Type { get; }

    /// <summary>Gets whether it is a flag, edited by a checkbox.</summary>
    public bool IsFlag => Type is VariableType.Flag;

    /// <summary>Gets whether it is a number, edited in a narrow field.</summary>
    public bool IsNumber => Type is VariableType.Number;

    /// <summary>Gets whether it is a text, edited in a field.</summary>
    public bool IsText => Type is VariableType.Text;

    /// <summary>Gets or sets a flag's value.</summary>
    [ObservableProperty]
    public partial bool Flag { get; set; }

    /// <summary>Gets or sets a number's or a text's value, as typed.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>Gets or sets whether the inspector's search shows the row.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; } = true;

    /// <summary>Initializes a new instance of the <see cref="VariableRowViewModel"/> class.</summary>
    /// <param name="variables">The characters' variables, which an edit sets.</param>
    /// <param name="character">The character.</param>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">Its value as JSON.</param>
    public VariableRowViewModel(CharacterVariables variables, CharacterKey character, string name, JsonNode value)
    {
        _variables = variables;
        _character = character;
        Name = name;
        Type = value.GetValueKind() switch
        {
            JsonValueKind.True or JsonValueKind.False => VariableType.Flag,
            JsonValueKind.Number => VariableType.Number,
            _ => VariableType.Text,
        };
        Read(value);
    }

    /// <summary>Reads the value again, as a behavior may have set it.</summary>
    /// <param name="value">Its value as JSON.</param>
    public void Read(JsonNode value)
    {
        _reading = true;

        if (IsFlag)
            Flag = value.GetValueKind() is JsonValueKind.True;
        else if (IsNumber)
            Text = double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);
        else
            Text = value.GetValueKind() is JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();

        _reading = false;
    }

    partial void OnFlagChanged(bool value)
    {
        if (!_reading)
            _variables.Set(_character, Name, JsonValue.Create(value));
    }

    partial void OnTextChanged(string value)
    {
        if (_reading)
            return;

        // A number that does not read is kept as typed, not set: the behavior keeps reading the last that did.
        if (IsNumber && double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number))
            _variables.Set(_character, Name, JsonValue.Create(number));
        else if (IsText)
            _variables.Set(_character, Name, JsonValue.Create(value));
    }
}
