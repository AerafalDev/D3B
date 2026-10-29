using System.Collections;
using System.Diagnostics.CodeAnalysis;
using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Lua;

namespace D3B.Scripting.Api;

/// <summary>The readable protocol as scripts see it: a message by its readable name, its fields by their JSON names, as Lua tables (D106).</summary>
/// <remarks>
/// <para>
/// A script sends the game's requests of D3B's readable protocol, whole (D55), named as the <c>.proto</c> files name them,
/// such as <c>MapMovementRequest</c>, their fields in lower camel case, such as <c>mapId</c>, as the rest of the API. Every
/// field is checked against the message's descriptor: an unknown field, a value of the wrong type, a number that is not
/// whole or does not fit, fail the call. Never raw bytes, never a message to the client.
/// </para>
/// <para>
/// What a script waits for, an event or a response of the readable protocol, comes back as a table of the fields the
/// readable protocol reads: a partial event's only (D55); an enumeration's value by its name.
/// </para>
/// </remarks>
internal static class ProtocolTables
{
    private static readonly Dictionary<string, MessageDescriptor> _messages = Messages();

    /// <summary>Finds a request of the readable game protocol, a message of its own whose name ends in <c>Request</c>.</summary>
    public static bool TryFindRequest(string name, [NotNullWhen(true)] out MessageDescriptor? descriptor)
    {
        return _messages.TryGetValue(name, out descriptor) && name.EndsWith("Request", StringComparison.Ordinal);
    }

    /// <summary>Finds what a script may wait for: an event or a response of the readable game protocol.</summary>
    public static bool TryFindAnswer(string name, [NotNullWhen(true)] out MessageDescriptor? descriptor)
    {
        return _messages.TryGetValue(name, out descriptor) && (name.EndsWith("Event", StringComparison.Ordinal) || name.EndsWith("Response", StringComparison.Ordinal));
    }

    /// <summary>Builds a message from a script's table.</summary>
    /// <param name="descriptor">The message.</param>
    /// <param name="fields">Its fields, by JSON name, or <see langword="null"/> for none.</param>
    /// <param name="error">Makes the error a field that does not fit raises.</param>
    public static IMessage Build(MessageDescriptor descriptor, LuaTable? fields, Func<string, Exception> error)
    {
        var message = descriptor.Parser.ParseFrom(ByteString.Empty);

        if (fields is null)
            return message;

        foreach (var pair in fields)
        {
            if (!pair.Key.TryRead<string>(out var key) || FieldOf(descriptor, key) is not { } field)
            {
                var known = string.Join(", ", descriptor.Fields.InFieldNumberOrder().Select(field => field.JsonName));

                throw error($"{descriptor.Name} has no field {pair.Key}; its fields are {(known.Length == 0 ? "none" : known)}");
            }

            if (field.IsMap)
                throw error($"{descriptor.Name}.{key} is a map, which a script does not fill yet");

            if (field.IsRepeated)
            {
                if (!pair.Value.TryRead<LuaTable>(out var items))
                    throw error($"{descriptor.Name}.{key} holds a list, given as a table, not a {pair.Value.TypeToString()}");

                var list = (IList)field.Accessor.GetValue(message);

                for (var index = 1; index <= items.ArrayLength; index++)
                    list.Add(ValueOf(field, items[index], $"{descriptor.Name}.{key}[{index}]", error));
            }
            else
            {
                field.Accessor.SetValue(message, ValueOf(field, pair.Value, $"{descriptor.Name}.{key}", error));
            }
        }

        return message;
    }

    /// <summary>Turns a message into a table of the fields its readable message reads.</summary>
    public static LuaTable ToTable(IMessage message)
    {
        var table = new LuaTable();

        foreach (var field in message.Descriptor.Fields.InFieldNumberOrder())
        {
            if (field.IsMap)
                continue;

            if (field.IsRepeated)
            {
                var items = new LuaTable();
                var index = 1;

                foreach (var item in (IList)field.Accessor.GetValue(message))
                    items[index++] = LuaOf(field, item);

                table[field.JsonName] = items;
            }
            else if (!field.HasPresence || field.Accessor.HasValue(message))
            {
                table[field.JsonName] = LuaOf(field, field.Accessor.GetValue(message));
            }
        }

        return table;
    }

    private static Dictionary<string, MessageDescriptor> Messages()
    {
        var messages = new Dictionary<string, MessageDescriptor>(StringComparer.Ordinal);

        foreach (var file in ReadableProtocol.Files.Where(file => file.Package == ReadableProtocol.GamePackage))
        {
            foreach (var message in file.MessageTypes)
                messages[message.Name] = message;
        }

        return messages;
    }

    private static FieldDescriptor? FieldOf(MessageDescriptor descriptor, string key)
    {
        return descriptor.Fields.InFieldNumberOrder().FirstOrDefault(field => field.JsonName == key);
    }

    private static object ValueOf(FieldDescriptor field, LuaValue value, string where, Func<string, Exception> error)
    {
        switch (field.FieldType)
        {
            case FieldType.Bool when value.TryRead<bool>(out var flag):
                return flag;

            case FieldType.String when value.TryRead<string>(out var text):
                return text;

            case FieldType.Float when value.TryRead<double>(out var single):
                return (float)single;

            case FieldType.Double when value.TryRead<double>(out var number):
                return number;

            case FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32:
                return (int)Whole(value, int.MinValue, int.MaxValue, where, field, error);

            case FieldType.UInt32 or FieldType.Fixed32:
                return (uint)Whole(value, uint.MinValue, uint.MaxValue, where, field, error);

            case FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64:
                return Whole(value, long.MinValue, long.MaxValue, where, field, error);

            case FieldType.Enum:
                {
                    var enumeration = field.EnumType;
                    var known = value.TryRead<string>(out var name)
                        ? enumeration.FindValueByName(name)
                        : enumeration.FindValueByNumber((int)Whole(value, int.MinValue, int.MaxValue, where, field, error));

                    return known is not null
                        ? Enum.ToObject(enumeration.ClrType, known.Number)
                        : throw error($"{where} is a {enumeration.Name}, which has no value {value}");
                }

            case FieldType.Message when value.TryRead<LuaTable>(out var nested):
                return Build(field.MessageType, nested, error);

            default:
                throw error($"{where} holds a {field.FieldType}, not a {value.TypeToString()}");
        }
    }

    private static long Whole(LuaValue value, double minimum, double maximum, string where, FieldDescriptor field, Func<string, Exception> error)
    {
        if (value.TryRead<double>(out var number) && number == Math.Floor(number) && number >= minimum && number <= maximum)
            return (long)number;

        throw error($"{where} holds a whole number that fits a {field.FieldType}, not {value}");
    }

    private static LuaValue LuaOf(FieldDescriptor field, object? value)
    {
        return value switch
        {
            null => LuaValue.Nil,
            IMessage message => ToTable(message),
            bool flag => flag,
            string text => text,
            Enum when field.FieldType is FieldType.Enum => field.EnumType.FindValueByNumber(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture))?.Name ?? (LuaValue)Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            int number => number,
            uint number => number,
            long number => number,
            ulong number => number,
            float number => number,
            double number => number,
            _ => value.ToString() ?? "",
        };
    }
}
