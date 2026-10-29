namespace D3B.Bot.Harvesting;

/// <summary>A skill the harvesting activity uses: the resource it gathers, the profession and level it needs, and the resource's priority.</summary>
/// <param name="SkillId">The skill.</param>
/// <param name="ItemId">The resource it gathers.</param>
/// <param name="JobId">The profession it belongs to.</param>
/// <param name="Level">The profession's level it needs.</param>
/// <param name="Priority">The resource's place among those asked for, 0 first; every resource 0 when the activity harvests everything it can.</param>
public readonly record struct HarvestTarget(int SkillId, int ItemId, int JobId, int Level, int Priority);
