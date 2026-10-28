using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using D3B.Protocol.Connection;
using D3B.Protocol.Mapping;
using Google.Protobuf;

namespace D3B.Protocol.Translation;

/// <summary>Reads a client build's wire through D3B's readable classes, once the build's mapping holds (D11, D47).</summary>
/// <remarks>
/// <para>
/// The readable classes read the wire as it is, since the obfuscation keeps every field's and value's number (D42). So a
/// build is translated only when its mapping holds the contract (D44) and pairs every readable field and enum value with
/// its own number. A build renumbering one is refused: its mapping holds both numbers, so rewriting them on the wire can
/// be added for the messages that change, once one does.
/// </para>
/// <para>
/// A frame is decoded once, its unknown fields dropped, and is understood only when its message encodes back to the
/// frame's length. One that does not holds a field the readable protocol lacks, or was encoded otherwise: D3B passes it
/// on as it is, and never rewrites it. An enum value the readable enum lacks keeps its number and encodes back as it
/// came, so nothing of it is lost; the audit notes the values a build adds.
/// </para>
/// </remarks>
public sealed class ProtocolTranslation
{
    private static readonly MessageParser<ConnectionMessage> _connection = ConnectionMessage.Parser.WithDiscardUnknownFields(true);

    /// <summary>Gets the client build it translates, as its version file names it.</summary>
    public string ClientVersion { get; }

    /// <summary>Gets what auditing the build's mapping checked.</summary>
    public AuditReport Audit { get; }

    private ProtocolTranslation(string clientVersion, AuditReport audit)
    {
        ClientVersion = clientVersion;
        Audit = audit;
    }

    /// <summary>Makes the translation of a build from its schema and its mapping, once they hold.</summary>
    /// <param name="schema">The build's schema.</param>
    /// <param name="mapping">The build's mapping, matched against that schema.</param>
    /// <returns>The translation.</returns>
    /// <exception cref="ProtocolMismatchException">The mapping fails the audit, or renumbers a readable field or value.</exception>
    public static ProtocolTranslation Create(ProtocolSchema schema, ProtocolMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(mapping);

        var audit = ContractAudit.Audit(ReadableProtocol.Files, schema, mapping);
        var failures = audit.Failures.Concat(Renumbered(mapping)).ToList();

        if (failures.Count > 0)
            throw new ProtocolMismatchException(mapping.ClientVersion, failures);

        return new ProtocolTranslation(mapping.ClientVersion, audit);
    }

    /// <summary>Lists the readable fields and enum values a mapping pairs with another number, which no translation reads.</summary>
    /// <param name="mapping">The mapping.</param>
    /// <returns>One line per field or value renumbered; none when the build keeps the wire's numbers.</returns>
    public static IReadOnlyList<string> Renumbered(ProtocolMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        var renumbered = new List<string>();

        foreach (var message in mapping.Messages)
        {
            foreach (var field in message.Fields.Where(field => field.Readable != field.Obfuscated))
                renumbered.Add($"{message.Readable} field {field.Readable} is field {field.Obfuscated} of .{message.Obfuscated}: the build renumbers it.");
        }

        foreach (var type in mapping.Enums)
        {
            foreach (var value in type.Values.Where(value => value.Readable != value.Obfuscated))
                renumbered.Add($"{type.Readable} value {value.Readable} is value {value.Obfuscated} of .{type.Obfuscated}: the build renumbers it.");
        }

        return renumbered;
    }

    /// <summary>Decodes a frame of the connection phase, its varint length already read (D38).</summary>
    /// <param name="frame">The frame's bytes.</param>
    /// <returns>The frame's message, and whether it was understood whole.</returns>
    [SuppressMessage("Performance", "CA1822", Justification = "Only a build whose mapping holds has a translation: reading the wire through one is what the version gate guards.")]
    public DecodedFrame<ConnectionMessage> DecodeConnection(ReadOnlySequence<byte> frame)
    {
        ConnectionMessage message;

        try
        {
            message = _connection.ParseFrom(frame);
        }
        catch (InvalidProtocolBufferException exception)
        {
            return new DecodedFrame<ConnectionMessage>(null, $"it is no connection message: {exception.Message}");
        }

        var length = message.CalculateSize();

        return length == frame.Length
            ? new DecodedFrame<ConnectionMessage>(message, null)
            : new DecodedFrame<ConnectionMessage>(message, $"its message encodes back to {length} of its {frame.Length} bytes: it holds what the readable protocol lacks, or is encoded otherwise.");
    }
}
