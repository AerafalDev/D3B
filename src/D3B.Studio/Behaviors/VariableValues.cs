using System.Text.Json.Nodes;

namespace D3B.Studio.Behaviors;

/// <summary>A variable's value as it is held, a <see cref="bool"/>, an <see cref="int"/> or a <see cref="string"/>, and as JSON.</summary>
internal static class VariableValues
{
    public static object Default(VariableType type)
    {
        return type switch
        {
            VariableType.Flag => false,
            VariableType.Number => 0,
            _ => "",
        };
    }

    public static bool TryRead(VariableType type, JsonNode? node, out object value)
    {
        value = Default(type);

        if (node is not JsonValue json)
            return node is null;

        switch (type)
        {
            case VariableType.Flag when json.TryGetValue<bool>(out var flag):
                value = flag;

                return true;
            case VariableType.Number when json.TryGetValue<int>(out var number):
                value = number;

                return true;
            case VariableType.Text when json.TryGetValue<string>(out var text):
                value = text;

                return true;
            default:
                return false;
        }
    }

    public static JsonNode Write(object value)
    {
        return value switch
        {
            bool flag => JsonValue.Create(flag),
            int number => JsonValue.Create(number),
            string text => JsonValue.Create(text),
            _ => throw new ArgumentException($"A variable holds no {value.GetType().Name}.", nameof(value)),
        };
    }
}
