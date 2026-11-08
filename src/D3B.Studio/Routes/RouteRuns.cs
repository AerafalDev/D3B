using System.Collections.Concurrent;
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

    /// <summary>Forgets the cursor of a run that ended.</summary>
    /// <param name="character">The character.</param>
    /// <param name="route">The route's id.</param>
    public void Forget(CharacterKey character, string route)
    {
        _runs.TryRemove((character, route), out _);
    }
}
