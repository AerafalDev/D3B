namespace D3B.App.ViewModels.Settings;

/// <summary>A character that plays the profile being edited, as its bot last saw it: its professions' levels and its map (D127).</summary>
/// <param name="Name">The character's name.</param>
/// <param name="JobLevels">Its professions' levels, by profession; empty while no bot saw them.</param>
/// <param name="MapId">The map it stands on, if its bot saw it.</param>
internal sealed record PlayerLevels(string Name, IReadOnlyDictionary<int, int> JobLevels, long? MapId);
