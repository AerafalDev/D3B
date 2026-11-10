using System.Collections.Concurrent;
using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Studio.Assets;

namespace D3B.Studio.Routes;

/// <summary>The cursors of the runs under way, one for each character and route (D379).</summary>
/// <remarks>
/// A cursor belongs to the run that made it, told by the moment the run began: a character that stops and starts again begins
/// its route over, where an interruption inside one run, a fight, a bank, goes on from where the route was cut.
/// </remarks>
public sealed class RouteRuns
{
    private readonly ConcurrentDictionary<(CharacterKey Character, string Route), (DateTimeOffset Started, RouteCursor Cursor)> _runs = new();
    private readonly ConcurrentDictionary<CharacterKey, Following> _following = new();

    private static RouteRunPosition? Read(CharacterKey character, Following following)
    {
        var at = following.Cursor.Snapshot();

        if (at.IsDone)
            return null;

        var route = following.Route;

        return new RouteRunPosition(character, route.Id, route.Name, route.Mode, route.Anchors.Length, at.Anchor, at.Arrived, at.Played, route.Anchors[at.Anchor].Actions.Length, at.Laps, at.Returning);
    }

    /// <summary>Gets the cursor of a run, made when it has none.</summary>
    /// <param name="character">The character.</param>
    /// <param name="route">The route.</param>
    /// <param name="started">When the run began.</param>
    /// <returns>The cursor.</returns>
    public RouteCursor Of(CharacterKey character, RouteAsset route, DateTimeOffset started)
    {
        ArgumentNullException.ThrowIfNull(route);

        var key = (character, route.Id);

        lock (_runs)
        {
            if (_runs.TryGetValue(key, out var run) && run.Started == started)
                return run.Cursor;

            var cursor = new RouteCursor(route.Anchors.Length, route.Mode);

            _runs[key] = (started, cursor);

            return cursor;
        }
    }

    /// <summary>Notes that a character follows a route now, so that the studio can show where it is.</summary>
    /// <remarks>The cursor outlives this: a run cut by a fight goes on from it, but is followed only while the route plays.</remarks>
    /// <param name="character">The character.</param>
    /// <param name="route">The route.</param>
    /// <param name="cursor">Where the run is, which the run moves.</param>
    public void Begin(CharacterKey character, RouteAsset route, RouteCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(cursor);

        _following[character] = new Following(route, cursor);
    }

    /// <summary>Notes that a character no longer follows its route: the route ended, was cut or failed.</summary>
    /// <param name="character">The character.</param>
    /// <param name="cursor">The cursor of the run that ended, so that a run begun since is left alone.</param>
    public void End(CharacterKey character, RouteCursor cursor)
    {
        if (_following.TryGetValue(character, out var following) && ReferenceEquals(following.Cursor, cursor))
            _following.TryRemove(new KeyValuePair<CharacterKey, Following>(character, following));
    }

    /// <summary>Gets where a character is on the route it follows now.</summary>
    /// <param name="character">The character.</param>
    /// <returns>Its place, or <see langword="null"/> while it follows none.</returns>
    public RouteRunPosition? Position(CharacterKey character)
    {
        return _following.TryGetValue(character, out var following) ? Read(character, following) : null;
    }

    /// <summary>Gets where every character is that follows a route now.</summary>
    /// <returns>Their places.</returns>
    public ImmutableArray<RouteRunPosition> Positions()
    {
        return [.. _following.Select(pair => Read(pair.Key, pair.Value)).OfType<RouteRunPosition>()];
    }

    /// <summary>Forgets the cursor of a run that ended.</summary>
    /// <param name="character">The character.</param>
    /// <param name="route">The route's id.</param>
    public void Forget(CharacterKey character, string route)
    {
        _runs.TryRemove((character, route), out _);
    }

    private sealed record Following(RouteAsset Route, RouteCursor Cursor);
}
