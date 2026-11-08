using D3B.Bot.Movement;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>What a route run does in the game: it goes to a map, plays an action and says what happened (D379).</summary>
/// <remarks>Apart from the run so that the course, the policies and the cursor are tried without a game.</remarks>
internal interface IRouteStage
{
    /// <summary>Tells whether the run plays an action.</summary>
    /// <param name="action">The action.</param>
    /// <returns>Whether it does today.</returns>
    bool CanPlay(NodeCall action);

    /// <summary>Goes to a map, by the character's own path: its zaaps, its doors, its levels.</summary>
    /// <param name="map">The map.</param>
    /// <param name="cancellationToken">Stops the journey.</param>
    /// <returns>How the journey ended.</returns>
    Task<TravelOutcome> TravelAsync(long map, CancellationToken cancellationToken);

    /// <summary>Plays an action on the map the character stands on.</summary>
    /// <param name="action">The action.</param>
    /// <param name="next">The map of the route's next step, which a zaap is taken towards; <see langword="null"/> at the last.</param>
    /// <param name="cancellationToken">Stops the action.</param>
    /// <returns>Whether it was played; one that could not be fails the route, the stage having said why in the journal.</returns>
    Task<bool> PlayAsync(NodeCall action, long? next, CancellationToken cancellationToken);

    /// <summary>Waits, by the bot's clock.</summary>
    /// <param name="time">How long.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes once the time passed.</returns>
    Task WaitAsync(TimeSpan time, CancellationToken cancellationToken);

    /// <summary>Writes a note in the bot's journal.</summary>
    /// <param name="key">The text's key in the behaviors' texts.</param>
    /// <param name="values">The values of its placeholders.</param>
    void Note(string key, params object[] values);
}
