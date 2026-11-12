using System.Security.Cryptography;
using System.Text.Json;

namespace D3B.Extractor.Protocol;

/// <summary>The protocol's schema of a build: its messages and the enums they hold, sorted by name (docs/protocol.md, "A new build").</summary>
/// <remarks>
/// Its digest is the SHA-256 of its canonical form: compact JSON, keys sorted, messages and enums by name, fields by
/// number, without the build, so that two builds of one schema share it and their pins can be carried over. The file
/// written for the maintainer is the same form, indented, with the build and the digest beside it.
/// </remarks>
internal sealed class ProtocolSchema
{
    /// <summary>The format of the schema's file and canonical form: a change updates every pinned build's digest (docs/protocol.md).</summary>
    public const int Format = 1;

    private static readonly JsonWriterOptions _canonical = new() { Indented = false };
    private static readonly JsonWriterOptions _file = new() { Indented = true, NewLine = "\n" };

    public required IReadOnlyList<SchemaMessage> Messages { get; init; }

    public required IReadOnlyList<SchemaEnum> Enums { get; init; }

    private static void WriteField(Utf8JsonWriter json, SchemaField field)
    {
        json.WriteStartObject();
        WriteIfAny(json, "key", field.Key);
        json.WriteString("kind", field.Kind);
        WriteIfAny(json, "label", field.Label);
        json.WriteNumber("number", field.Number);
        WriteIfAny(json, "oneof", field.Oneof);
        WriteIfAny(json, "type", field.Type);
        WriteIfAny(json, "value", field.Value);
        json.WriteEndObject();
    }

    private static void WriteIfAny(Utf8JsonWriter json, string name, string? value)
    {
        if (value is not null)
        {
            json.WriteString(name, value);
        }
    }

    /// <summary>The schema's canonical form, the bytes its digest is taken from.</summary>
    public byte[] Canonical()
    {
        return Write(null, null, _canonical);
    }

    /// <summary>The SHA-256 of the canonical form, in lowercase hexadecimal.</summary>
    public string Digest()
    {
        return Convert.ToHexStringLower(SHA256.HashData(Canonical()));
    }

    /// <summary>The file of the schema of <paramref name="build"/>: its canonical form, indented, with the build and the digest.</summary>
    public byte[] File(string build)
    {
        return Write(build, Digest(), _file);
    }

    /// <summary>Writes the file of <paramref name="build"/> at <paramref name="path"/>, replacing whatever was there whole.</summary>
    public void WriteTo(string path, string build)
    {
        var staging = path + ".d3b-staging";

        System.IO.File.WriteAllBytes(staging, File(build));
        System.IO.File.Move(staging, path, overwrite: true);
    }

    // Keys are written in their sorted order, by hand, so that the form never depends on a serializer's choices.
    private byte[] Write(string? build, string? digest, JsonWriterOptions options)
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer, options))
        {
            json.WriteStartObject();

            if (build is not null)
            {
                json.WriteString("build", build);
            }

            json.WriteStartArray("enums");

            foreach (var schemaEnum in Enums.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteString("name", schemaEnum.Name);
                json.WriteStartArray("values");

                foreach (var value in schemaEnum.Values)
                {
                    json.WriteNumberValue(value);
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteNumber("format", Format);
            json.WriteStartArray("messages");

            foreach (var message in Messages.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteStartArray("fields");

                foreach (var field in message.Fields.OrderBy(item => item.Number))
                {
                    WriteField(json, field);
                }

                json.WriteEndArray();
                json.WriteString("name", message.Name);
                json.WriteEndObject();
            }

            json.WriteEndArray();

            if (digest is not null)
            {
                json.WriteString("sha256", digest);
            }

            json.WriteEndObject();
        }

        if (options.Indented)
        {
            buffer.WriteByte((byte)'\n');
        }

        return buffer.ToArray();
    }
}
