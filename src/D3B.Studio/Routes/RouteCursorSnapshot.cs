namespace D3B.Studio.Routes;

/// <summary>Where a run is on its route, read in one go, so that the app never sees an anchor of one moment and the arrival of another (D419).</summary>
/// <param name="Anchor">The anchor gone to, or -1 once the run is done.</param>
/// <param name="Arrived">Whether the character is on the anchor's map, as opposed to on its way there.</param>
/// <param name="Played">How many of the anchor's actions were played.</param>
/// <param name="Visits">How many anchors the run has been to.</param>
/// <param name="Laps">How many laps of a loop the run has finished.</param>
/// <param name="Returning">Whether a route there and back is on its way back.</param>
/// <param name="IsDone">Whether the run has been to every anchor its course asks for.</param>
public readonly record struct RouteCursorSnapshot(int Anchor, bool Arrived, int Played, long Visits, long Laps, bool Returning, bool IsDone);
