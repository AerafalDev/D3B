using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace D3B.Bot.Fights;

/// <summary>The placement cells of every map the host's bots fought on, each set the server gave kept apart (9f3).</summary>
/// <remarks>
/// One per host, which <see cref="FightPlacementLearner"/> teaches from every fight any bot begins, whoever starts it; D3B's
/// database keeps them from one run to the next. A map may give more than one set: each is its own, counted.
/// </remarks>
public sealed class FightPlacements
{
    private readonly ConcurrentDictionary<(long MapId, string Challengers, string Defenders), FightPlacement> _placements = new();

    /// <summary>Raised when a fight began with a set of cells, with that set as it now stands.</summary>
    /// <remarks>Raised on the thread that saw it, a relay's read loop: a handler must not wait.</remarks>
    public event Action<FightPlacement>? Learned;

    /// <summary>Gets how many sets of cells are known, on every map.</summary>
    public int Count => _placements.Count;

    /// <summary>Takes back what was seen before, such as what the database kept, without raising <see cref="Learned"/>.</summary>
    /// <param name="placement">The set of cells.</param>
    public void Remember(FightPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        _placements[KeyOf(placement.MapId, placement.ChallengerCells, placement.DefenderCells)] = placement;
    }

    /// <summary>Gets the sets of cells known on a map.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>The sets, the most seen first.</returns>
    public IReadOnlyList<FightPlacement> Of(long mapId)
    {
        return [.. _placements.Values.Where(placement => placement.MapId == mapId).OrderByDescending(placement => placement.Seen)];
    }

    /// <summary>Counts a fight that began on a map with a set of cells.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="challengerCells">The challengers' cells, red.</param>
    /// <param name="defenderCells">The defenders' cells, blue.</param>
    /// <param name="at">When it began.</param>
    /// <returns>The set as it now stands.</returns>
    public FightPlacement Seen(long mapId, IEnumerable<int> challengerCells, IEnumerable<int> defenderCells, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(challengerCells);
        ArgumentNullException.ThrowIfNull(defenderCells);

        ImmutableArray<int> challengers = [.. challengerCells.Distinct().Order()];
        ImmutableArray<int> defenders = [.. defenderCells.Distinct().Order()];
        var placement = _placements.AddOrUpdate(
            KeyOf(mapId, challengers, defenders),
            _ => new FightPlacement(mapId, challengers, defenders, 1, at, at),
            (_, known) => known with { Seen = known.Seen + 1, LastSeen = at > known.LastSeen ? at : known.LastSeen });

        Learned?.Invoke(placement);

        return placement;
    }

    private static (long, string, string) KeyOf(long mapId, ImmutableArray<int> challengers, ImmutableArray<int> defenders)
    {
        return (mapId, string.Join(',', challengers), string.Join(',', defenders));
    }
}
