using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Tests.Mapping;

/// <summary>
/// A schema made from readable files as a build's is made from the client's: one file without a package, every message,
/// enum, field, oneof and value renamed, and the order of the top-level types reversed. Field and value numbers stay, as
/// the client's obfuscation keeps them.
/// </summary>
/// <remarks>Maps are not handled: no readable file holds one yet.</remarks>
internal sealed class Obfuscation
{
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private int _counter;

    /// <summary>Gets the obfuscated file, which a test may change before building the schema.</summary>
    public FileDescriptorProto File { get; }

    public Obfuscation(IEnumerable<FileDescriptor> files, string fileName = ReadableProtocol.ConnectionSchemaFile)
    {
        var protos = files.Select(file => file.ToProto()).ToList();

        File = new FileDescriptorProto { Name = fileName, Syntax = "proto3" };

        foreach (var proto in protos)
        {
            foreach (var message in proto.MessageType)
                Name(message, Prefix(proto), ".");

            foreach (var type in proto.EnumType)
                _names[Prefix(proto) + type.Name] = "." + Next("t");
        }

        foreach (var proto in protos)
        {
            File.MessageType.AddRange(proto.MessageType.Select(message => Rename(message, Prefix(proto))));
            File.EnumType.AddRange(proto.EnumType.Select(type => Rename(type, Prefix(proto) + type.Name)));
        }

        var messages = File.MessageType.Reverse().ToList();
        var enums = File.EnumType.Reverse().ToList();

        File.MessageType.Clear();
        File.MessageType.AddRange(messages);
        File.EnumType.Clear();
        File.EnumType.AddRange(enums);
    }

    private static string Prefix(FileDescriptorProto proto)
    {
        return proto.Package.Length is 0 ? "." : "." + proto.Package + ".";
    }

    /// <summary>Gets the obfuscated full name of a readable message or enum, without the leading dot.</summary>
    public string Of(string readableFullName)
    {
        return _names["." + readableFullName][1..];
    }

    /// <summary>Gets an obfuscated message by the readable full name it was made from.</summary>
    public DescriptorProto Message(string readableFullName)
    {
        var path = Of(readableFullName).Split('.');
        var message = File.MessageType.Single(candidate => candidate.Name == path[0]);

        foreach (var name in path.Skip(1))
            message = message.NestedType.Single(candidate => candidate.Name == name);

        return message;
    }

    /// <summary>Gets an obfuscated enum by the readable full name it was made from.</summary>
    public EnumDescriptorProto Enum(string readableFullName)
    {
        var path = Of(readableFullName).Split('.');

        if (path.Length is 1)
            return File.EnumType.Single(candidate => candidate.Name == path[0]);

        var parent = Message(string.Join('.', readableFullName.Split('.')[..^1]));

        return parent.EnumType.Single(candidate => candidate.Name == path[^1]);
    }

    /// <summary>Builds the schema of the obfuscated file as it stands.</summary>
    public ProtocolSchema Schema()
    {
        return ProtocolSchema.Load(new FileDescriptorSet { File = { File } }.ToByteArray());
    }

    private void Name(DescriptorProto message, string readablePrefix, string obfuscatedPrefix)
    {
        var full = readablePrefix + message.Name;
        var name = obfuscatedPrefix + Next("t");

        _names[full] = name;

        foreach (var nested in message.NestedType)
            Name(nested, full + ".", name + ".");

        foreach (var type in message.EnumType)
            _names[full + "." + type.Name] = name + "." + Next("t");
    }

    private DescriptorProto Rename(DescriptorProto message, string prefix)
    {
        var full = prefix + message.Name;
        var renamed = message.Clone();

        renamed.Name = _names[full].Split('.')[^1];
        renamed.NestedType.Clear();
        renamed.NestedType.AddRange(message.NestedType.Select(nested => Rename(nested, full + ".")));
        renamed.EnumType.Clear();
        renamed.EnumType.AddRange(message.EnumType.Select(type => Rename(type, full + "." + type.Name)));

        foreach (var field in renamed.Field)
        {
            field.Name = Next("f");
            field.ClearJsonName();

            if (field.HasTypeName)
                field.TypeName = _names[field.TypeName];
        }

        for (var index = 0; index < renamed.OneofDecl.Count; index++)
        {
            var synthetic = renamed.Field.FirstOrDefault(field => field.Proto3Optional && field.OneofIndex == index);

            renamed.OneofDecl[index].Name = synthetic is null ? Next("o") : "_" + synthetic.Name;
        }

        return renamed;
    }

    private EnumDescriptorProto Rename(EnumDescriptorProto type, string full)
    {
        var renamed = type.Clone();

        renamed.Name = _names[full].Split('.')[^1];

        foreach (var value in renamed.Value)
            value.Name = Next("v");

        renamed.Options = null;

        return renamed;
    }

    private string Next(string prefix)
    {
        return $"{prefix}{++_counter:D4}";
    }
}
