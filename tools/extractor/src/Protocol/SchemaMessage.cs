namespace D3B.Extractor.Protocol;

/// <summary>A message of the schema: its name, three obfuscated letters for a top-level one, and its fields by number.</summary>
internal sealed class SchemaMessage
{
    public required string Name { get; init; }

    public required IReadOnlyList<SchemaField> Fields { get; init; }
}
