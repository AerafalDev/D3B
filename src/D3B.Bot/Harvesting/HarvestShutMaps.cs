using D3B.Bot.Characters;

namespace D3B.Bot.Harvesting;

/// <summary>The maps the harvest's tours leave out for what the character is: those no way leads to (D571) and those a group beat it on (D582).</summary>
/// <remarks>
/// <para>
/// The activity keeps them for all its tours, through a restart after a failure and a sub-area drawn again (D581): a tour's own
/// sets begin anew with it, and the maps no way leads to were sought again at each new tour, each one a journey with no way.
/// </para>
/// <para>
/// A level gained, of the character or of a profession, a subscription taken or a zaap learned may open a way the graph's criteria
/// or its teleports shut: the maps no way led to are sought again. A map a group attacked and beat the character on comes back
/// once the character reaches the level asked, the highest monster's it fought there less 20 levels, from which the owner says such a
/// monster no longer attacks it.
/// </para>
/// </remarks>
internal sealed class HarvestShutMaps
{
    /// <summary>How many levels below a monster a character no longer gets attacked by it: the owner's figure, not measured (D582).</summary>
    public const int AggressionGap = 20;

    private readonly HashSet<long> _noWay = [];
    private readonly Dictionary<long, int> _lost = [];
    private (int? Level, bool Subscribed, int JobLevels, int Zaaps)? _noWayFor;

    /// <summary>Gets the level from which a map a group beat the character on comes back: the highest monster's of the fight less <see cref="AggressionGap"/>, the character's next level at the least.</summary>
    /// <param name="highestMonster">The highest level of the monsters the character fought, 0 when none was shown.</param>
    /// <param name="level">The character's level.</param>
    /// <returns>The level.</returns>
    public static int LevelAsked(int highestMonster, int? level)
    {
        return Math.Max(highestMonster - AggressionGap, (level ?? 0) + 1);
    }

    /// <summary>Takes what the character is now, forgetting the maps a change may open.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="now">The bots' clock's time, which the subscription is read at.</param>
    public void Know(CharacterState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);

        var facts = (state.Level, state.IsSubscribedAt(now), state.Jobs.Values.Sum(job => job.Level), state.KnownZaaps.Count);

        if (_noWayFor is { } known && known != facts)
            _noWay.Clear();

        foreach (var (mapId, level) in _lost.Where(lost => state.Level >= lost.Value).ToList())
            _lost.Remove(mapId);

        _noWayFor = facts;
    }

    /// <summary>Marks a map no way of the world graph leads to from where the character stands, by its rules.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Whether it was not marked yet.</returns>
    public bool NoWay(long mapId)
    {
        return _noWay.Add(mapId);
    }

    /// <summary>Marks a map a group attacked and beat the character on, until the character reaches a level.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="level">The character's level from which the map comes back.</param>
    public void Lost(long mapId, int level)
    {
        _lost[mapId] = Math.Max(level, _lost.GetValueOrDefault(mapId));
    }

    /// <summary>Gets whether a group beat the character on a map, below the level that brings it back.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Whether it did.</returns>
    public bool IsLost(long mapId)
    {
        return _lost.ContainsKey(mapId);
    }

    /// <summary>Gets whether the tours leave a map out.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Whether they do.</returns>
    public bool Contains(long mapId)
    {
        return _noWay.Contains(mapId) || _lost.ContainsKey(mapId);
    }
}
