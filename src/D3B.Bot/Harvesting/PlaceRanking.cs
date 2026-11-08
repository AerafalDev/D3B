namespace D3B.Bot.Harvesting;

/// <summary>What <see cref="AutomaticPlace"/> ranked for a character: the sub-areas it may harvest in, and where each profession's best resource grows (D391).</summary>
/// <param name="Candidates">The sub-areas, the richest first.</param>
/// <param name="Tops">By profession, the sub-area holding the most maps of the highest-level resource the character can harvest of it; a profession with none placed has no entry.</param>
public sealed record PlaceRanking(IReadOnlyList<PlaceCandidate> Candidates, IReadOnlyDictionary<int, int> Tops);
