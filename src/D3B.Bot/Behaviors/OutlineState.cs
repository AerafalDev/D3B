namespace D3B.Bot.Behaviors;

/// <summary>A state of a behavior as the studio lists it: its name, how deep it sits, and whether it is urgent (D325).</summary>
/// <param name="Id">The state's id.</param>
/// <param name="Name">Its name, its id when it has none.</param>
/// <param name="Depth">How deep it sits under the layer's states, 0 for one of them.</param>
/// <param name="IsUrgent">Whether it cuts the step running once it holds.</param>
public sealed record OutlineState(string Id, string Name, int Depth, bool IsUrgent);
