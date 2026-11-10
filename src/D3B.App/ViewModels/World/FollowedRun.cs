using Avalonia.Media;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.World;

/// <summary>A character that follows a route now, with what the studio draws it by (D419).</summary>
/// <param name="Position">Where it is on the route.</param>
/// <param name="Name">The character's name.</param>
/// <param name="Initials">The letters of its marker.</param>
/// <param name="Color">Its breed's colour.</param>
internal sealed record FollowedRun(RouteRunPosition Position, string Name, string Initials, IBrush? Color);
