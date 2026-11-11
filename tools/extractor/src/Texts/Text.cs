namespace D3B.Extractor.Texts;

/// <summary>One text of the client in its five languages, as the data folder writes it inline (ADR 0008).</summary>
internal sealed class Text
{
    public required string Fr { get; init; }

    public required string En { get; init; }

    public required string De { get; init; }

    public required string Es { get; init; }

    public required string Pt { get; init; }
}
