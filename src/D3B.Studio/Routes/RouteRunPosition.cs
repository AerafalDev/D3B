using D3B.Bot.Characters;
using D3B.Studio.Assets;

namespace D3B.Studio.Routes;

/// <summary>Where a character is on the route it follows now: what the studio shows of a run (D419).</summary>
/// <param name="Character">The character.</param>
/// <param name="Route">The route's id.</param>
/// <param name="Name">The route's name.</param>
/// <param name="Mode">How the route is gone through.</param>
/// <param name="Anchors">How many steps the route has.</param>
/// <param name="Anchor">The step gone to, counted from 0.</param>
/// <param name="Arrived">Whether the character is on the step's map, as opposed to on its way there.</param>
/// <param name="Played">How many of the step's actions were played.</param>
/// <param name="Actions">How many actions the step holds.</param>
/// <param name="Laps">How many laps of a loop the run has finished.</param>
/// <param name="Returning">Whether a route there and back is on its way back.</param>
public sealed record RouteRunPosition(CharacterKey Character, string Route, string Name, RouteMode Mode, int Anchors, int Anchor, bool Arrived, int Played, int Actions, long Laps, bool Returning)
{
    /// <summary>Gets the step gone to as the studio numbers it, from 1.</summary>
    public int Step => Anchor + 1;

    /// <summary>Gets the lap of a loop the run is on, from 1.</summary>
    public long Lap => Laps + 1;
}
