namespace D3B.Extractor.Protocol;

/// <summary>The protocol's schema of a build: its messages and the enums they hold, sorted by name (docs/protocol.md, "A new build").</summary>
internal sealed class ProtocolSchema
{
    public required IReadOnlyList<SchemaMessage> Messages { get; init; }

    public required IReadOnlyList<SchemaEnum> Enums { get; init; }
}
