using System.Collections.Concurrent;
using D3B.Bot.Characters;

namespace D3B.Studio.Behaviors;

/// <summary>The lists of steps a character plays as steps of others, each with the place it has reached (D421).</summary>
/// <remarks>
/// As <see cref="Routes.RouteRuns"/> does for a route: a run cut by a fight or a bank goes on from the step it was at, and a
/// character that stops and starts again begins over, told by the moment the run began.
/// </remarks>
internal sealed class SequenceRuns
{
    private readonly ConcurrentDictionary<(CharacterKey Character, string Sequence), (DateTimeOffset Started, StateVisit Visit)> _runs = new();
    private readonly ConcurrentDictionary<(CharacterKey Character, string Sequence), bool> _playing = new();

    /// <summary>Gets the visit of a list, made when the character has none under way in this run.</summary>
    /// <param name="character">The character.</param>
    /// <param name="sequence">The list's behavior id.</param>
    /// <param name="started">When the run began.</param>
    /// <param name="make">Makes a visit, when the list has none yet.</param>
    /// <returns>The visit.</returns>
    public StateVisit Of(CharacterKey character, string sequence, DateTimeOffset started, Func<StateVisit> make)
    {
        ArgumentNullException.ThrowIfNull(make);

        var key = (character, sequence);

        lock (_runs)
        {
            if (_runs.TryGetValue(key, out var run))
            {
                if (run.Started == started)
                    return run.Visit;

                run.Visit.Dispose();
            }

            var visit = make();

            _runs[key] = (started, visit);

            return visit;
        }
    }

    /// <summary>Notes that a character plays a list now.</summary>
    /// <param name="character">The character.</param>
    /// <param name="sequence">The list's behavior id.</param>
    /// <returns><see langword="false"/> when it already plays it: the list plays itself.</returns>
    public bool TryBegin(CharacterKey character, string sequence)
    {
        return _playing.TryAdd((character, sequence), true);
    }

    /// <summary>Notes that a character no longer plays a list: it ended, was cut or failed. Its place is kept.</summary>
    /// <param name="character">The character.</param>
    /// <param name="sequence">The list's behavior id.</param>
    public void End(CharacterKey character, string sequence)
    {
        _playing.TryRemove((character, sequence), out _);
    }

    /// <summary>Forgets the place of a list that was played to its end.</summary>
    /// <param name="character">The character.</param>
    /// <param name="sequence">The list's behavior id.</param>
    public void Forget(CharacterKey character, string sequence)
    {
        if (_runs.TryRemove((character, sequence), out var run))
            run.Visit.Dispose();
    }
}
