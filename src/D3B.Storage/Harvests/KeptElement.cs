namespace D3B.Storage.Harvests;

/// <summary>An element of a map that gathers a resource, as a bot saw it, without whether it offered its skill then (D584).</summary>
/// <param name="ElementId">Its identifier on the map.</param>
/// <param name="SkillId">The skill that gathers it.</param>
/// <param name="ItemId">The resource it gives.</param>
public sealed record KeptElement(int ElementId, int SkillId, int ItemId);
