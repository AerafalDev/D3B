namespace D3B.App.ViewModels.World;

/// <summary>The characters that follow a route now, read again at each tick by what shows them: the Outliner, the map and the Sequencer (D419).</summary>
internal sealed class FollowedRuns
{
    private readonly Func<IEnumerable<FollowedRun>> _source;

    /// <summary>Initializes a new instance of the <see cref="FollowedRuns"/> class.</summary>
    /// <param name="source">Reads the characters that follow a route.</param>
    public FollowedRuns(Func<IEnumerable<FollowedRun>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
    }

    /// <summary>Gets what follows nothing.</summary>
    public static FollowedRuns None { get; } = new(() => []);

    /// <summary>Gets every character that follows a route now.</summary>
    /// <returns>The characters, in the order they were read.</returns>
    public IReadOnlyList<FollowedRun> All()
    {
        return [.. _source()];
    }

    /// <summary>Gets the characters that follow a route now.</summary>
    /// <param name="route">The route's id.</param>
    /// <returns>The characters on it.</returns>
    public IReadOnlyList<FollowedRun> On(string route)
    {
        return [.. _source().Where(run => run.Position.Route == route)];
    }
}
