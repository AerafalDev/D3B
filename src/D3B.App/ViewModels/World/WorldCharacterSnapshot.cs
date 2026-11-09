using Avalonia.Media;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.World;

/// <summary>What the viewport reads of a character online: who it is, where it stands and whether it plays (D397).</summary>
/// <param name="Key">The character.</param>
/// <param name="Name">Its name.</param>
/// <param name="Initials">The letters of its disc.</param>
/// <param name="Color">Its breed's colour.</param>
/// <param name="MapId">The map it stands on, or <see langword="null"/> while it is on none.</param>
/// <param name="Playing">Whether its activity runs or it fights.</param>
internal sealed record WorldCharacterSnapshot(CharacterKey Key, string Name, string Initials, IBrush? Color, long? MapId, bool Playing);
