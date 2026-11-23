using D3B.Extractor.Texts;

namespace D3B.Extractor.Npcs;

/// <summary>A question or a reply of a dialog: the id the server names it by, and its text.</summary>
internal sealed class NpcLine
{
    public required int Id { get; init; }

    /// <summary>Gets its text, or <see langword="null"/> when a language lacks it (190 replies and 112 questions on 3.7.14.18, kept by the owner's choice of 2026-11-23).</summary>
    public required Text? Text { get; init; }
}
