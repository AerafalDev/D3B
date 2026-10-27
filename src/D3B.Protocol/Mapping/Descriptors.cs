using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>Walks descriptors: every message and enum of a file, nested ones included.</summary>
internal static class Descriptors
{
    /// <summary>Every message of a list and the messages nested in them, map entries included, parents first.</summary>
    public static IEnumerable<MessageDescriptor> Messages(IEnumerable<MessageDescriptor> messages)
    {
        foreach (var message in messages)
        {
            yield return message;

            foreach (var nested in Messages(message.NestedTypes))
                yield return nested;
        }
    }

    /// <summary>Every enum of a file, nested ones included.</summary>
    public static IEnumerable<EnumDescriptor> Enums(FileDescriptor file)
    {
        return file.EnumTypes.Concat(Messages(file.MessageTypes).SelectMany(message => message.EnumTypes));
    }

    /// <summary>Tells whether a message is the entry protobuf describes a map with, which no <c>.proto</c> declares.</summary>
    public static bool IsMapEntry(MessageDescriptor message)
    {
        return message.ToProto().Options?.MapEntry is true;
    }

    /// <summary>Tells whether a field is a member of a oneof written in the <c>.proto</c>, not of a proto3 <c>optional</c> field's.</summary>
    public static bool InRealOneof(FieldDescriptor field)
    {
        return field.RealContainingOneof is not null;
    }

    /// <summary>Tells whether a field is a proto3 <c>optional</c> field, which protoc gives a synthetic oneof.</summary>
    public static bool IsOptional(FieldDescriptor field)
    {
        return field.ContainingOneof is { IsSynthetic: true };
    }

    /// <summary>Describes a field's type as <c>.proto</c> text declares it, for messages naming what failed.</summary>
    public static string TypeOf(FieldDescriptor field)
    {
        var type = field.FieldType switch
        {
            FieldType.Message or FieldType.Group => field.MessageType.FullName,
            FieldType.Enum => field.EnumType.FullName,
            _ => field.FieldType.ToString().ToLowerInvariant(),
        };

        return field.IsRepeated ? "repeated " + type : IsOptional(field) ? "optional " + type : type;
    }
}
