namespace D3B.Extractor.Protocol;

/// <summary>An enum a field of the schema holds: its name and its values, in order.</summary>
internal sealed class SchemaEnum
{
    public required string Name { get; init; }

    public required IReadOnlyList<long> Values { get; init; }
}
