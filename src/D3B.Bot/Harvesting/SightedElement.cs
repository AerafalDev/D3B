namespace D3B.Bot.Harvesting;

/// <summary>An element of a map that gathers a resource, as a bot saw it (D145).</summary>
/// <param name="ElementId">Its identifier on the map.</param>
/// <param name="SkillId">The skill that gathers it.</param>
/// <param name="ItemId">The resource it gives.</param>
/// <param name="IsEnabled">Whether it offered the skill then: not, from its harvest until it grows back.</param>
public readonly record struct SightedElement(int ElementId, int SkillId, int ItemId, bool IsEnabled);
