using D3B.Bot.Activities;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Maps;

namespace D3B.Samples.Hello;

/// <summary>A sample activity: walks the character, as many times as its settings say, to a cell near it chosen at random on its map.</summary>
/// <remarks>
/// Each step is chosen from the state when it starts, as every activity's (D87): after an interruption, the stroll goes on
/// from wherever the character stands. It keeps clear of the cells that send the character to another map, which the
/// world graph names. A walk the character is too busy for is not sent, and would come back at once: the stroll waits
/// instead, rather than step again and again.
/// </remarks>
internal sealed class Stroll : BotActivity
{
    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(5);

    private readonly StrollSettings _settings;
    private readonly StrollJournal _journal;
    private int _walked;

    /// <summary>Gets how many walks the stroll takes.</summary>
    public int Length => _settings.Walks;

    /// <inheritdoc/>
    public override string Name => "Stroll";

    /// <summary>Gets how many walks the stroll took so far, arrived or not.</summary>
    public int Walked => Volatile.Read(ref _walked);

    /// <summary>Initializes a new instance of the <see cref="Stroll"/> class.</summary>
    /// <param name="settings">How many walks it takes, and how far each goes, as a profile or the defaults say.</param>
    /// <param name="journal">Where the plugin's page reads what the stroll did.</param>
    public Stroll(StrollSettings settings, StrollJournal journal)
    {
        _settings = settings;
        _journal = journal;
    }

    private static List<int> Destinations(BotContext context, MapData map, int from, int reach)
    {
        var leaving = new HashSet<int>();

        // A map action moves whoever steps on its cell (D73): a stroll walks around them.
        if (context.World.TryGetVertex(map, from, out var vertex))
        {
            foreach (var edge in context.World.Graph.EdgesFrom(vertex))
            {
                foreach (var transition in context.World.Graph.TransitionsOf(edge))
                {
                    if (transition.Type is WorldTransitionType.MapAction)
                        leaving.Add(transition.CellId);
                }
            }
        }

        var zone = map.Cell(from).Zone;

        return [.. Enumerable.Range(0, MapData.CellCount).Where(cell => cell != from
            && MapGeometry.Distance(from, cell) <= reach
            && map.Cell(cell) is { IsWalkable: true } target
            && target.Zone == zone
            && !leaving.Contains(cell))];
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is not { } from || !context.Maps.TryGet(mapId, out var map))
            return true;

        var destinations = Destinations(context, map, from, _settings.Reach);

        // Nowhere to go: the stroll is over.
        if (destinations.Count == 0)
            return false;

        var cell = destinations[Random.Shared.Next(destinations.Count)];
        var outcome = await context.WalkAsync(cell, cancellationToken).ConfigureAwait(false);

        if (outcome is WalkOutcome.Busy)
        {
            await context.WaitUntilAsync(now => !now.IsBusy, _busyDelay, cancellationToken).ConfigureAwait(false);

            return true;
        }

        _journal.Record(state.Key, cell, outcome);

        return Interlocked.Increment(ref _walked) < _settings.Walks;
    }
}
