namespace D3B.Bot.Activities;

/// <summary>The zones of the project, which an activity whose place is a zone goes through (D415).</summary>
/// <remarks>
/// The zones are the studio's files, which <c>D3B.Bot</c> does not see (D393): whoever holds the project implements this and the host
/// gives it to the activities. An activity reads a zone when its run starts, so that a zone changed while it plays is read at the next.
/// </remarks>
public interface IPlaceZones
{
    /// <summary>Finds the maps a zone holds.</summary>
    /// <param name="zone">The zone's id.</param>
    /// <param name="maps">The maps, by their ids, when the zone is in the project; empty when it holds none.</param>
    /// <returns>Whether the project holds the zone.</returns>
    bool TryResolve(string zone, out IReadOnlySet<long> maps);
}
