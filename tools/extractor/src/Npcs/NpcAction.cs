using D3B.Extractor.Texts;

namespace D3B.Extractor.Npcs;

/// <summary>An action a character may take on a non-player character, such as 3, Parler, as <c>NpcActionData</c> holds it.</summary>
internal sealed class NpcAction
{
    public required int Id { get; init; }

    /// <summary>Gets the action it stands for: 17, Examiner, is 3, as 3 itself is (3.7.14.18).</summary>
    public required int RealId { get; init; }

    public required Text Name { get; init; }
}
