using System.Globalization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>A route run in the game: the character goes by its own path, and the actions are the nodes of the catalog (D379).</summary>
internal sealed class GameRouteStage : IRouteStage
{
    private readonly ActionContext _context;
    private readonly BotContext _bot;
    private readonly NodeCatalog _nodes;

    /// <summary>Initializes a new instance of the <see cref="GameRouteStage"/> class.</summary>
    /// <param name="context">The step's context.</param>
    /// <param name="bot">The bot's context.</param>
    /// <param name="nodes">The kinds of nodes.</param>
    public GameRouteStage(ActionContext context, BotContext bot, NodeCatalog nodes)
    {
        _context = context;
        _bot = bot;
        _nodes = nodes;
    }

    /// <inheritdoc/>
    public bool CanPlay(NodeCall action)
    {
        return RouteActions.IsPlayed(action.Node) && _nodes.TryGet(action.Node, out _);
    }

    /// <inheritdoc/>
    public Task<TravelOutcome> TravelAsync(long map, CancellationToken cancellationToken)
    {
        return _bot.TravelAsync(new HashSet<long> { map }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> PlayAsync(NodeCall action, long? next, CancellationToken cancellationToken)
    {
        if (!_nodes.TryGet(action.Node, out var definition))
            throw new InvalidOperationException($"The route holds the action {action.Node}, which the host does not know.");

        try
        {
            if (action.Node == RouteActions.Zaap)
            {
                await TakeZaapAsync(next, cancellationToken).ConfigureAwait(false);

                return true;
            }

            var values = RouteActionCalls.Read(definition, action).Settings ?? new object();

            await ((IActionNode)_nodes.CodeOf(definition)).RunAsync(_context, values, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (RouteActionException failed)
        {
            Note("RouteActionFailed", failed.Node, _bot.State.MapId ?? 0, failed.Outcome);

            return false;
        }
    }

    /// <inheritdoc/>
    public Task WaitAsync(TimeSpan time, CancellationToken cancellationToken)
    {
        return Task.Delay(time, _bot.Time, cancellationToken);
    }

    /// <inheritdoc/>
    public void Reached(string route, int step, int steps, long map)
    {
        Note("RouteStepReached", route, step, steps, map);
    }

    /// <inheritdoc/>
    public void Note(string key, params object[] values)
    {
        var culture = CultureInfo.CurrentUICulture;

        _context.Note(string.Format(culture, BehaviorTexts.Resources.GetString(key, culture) ?? key, values));
    }

    // The zaap nearest the next step, as the pathfinder reckons: a path there that begins with a teleport saving at least one change.
    private async Task TakeZaapAsync(long? next, CancellationToken cancellationToken)
    {
        var state = _bot.State;

        if (next is not { } map)
        {
            Note("RouteZaapNoNext");

            return;
        }

        if (state.MapId is not { } here || state.Cell is not { } cell || state.IsBusy || !_bot.Maps.TryGet(here, out var data) || !_bot.World.TryGetVertex(data, cell, out var vertex))
            throw new RouteActionException(RouteActions.Zaap, nameof(MapChangeOutcome.Busy));

        var rules = WorldTravel.RulesOf(_bot) with { TeleportChanges = 1 };

        if (!_bot.World.TryFind(vertex, new HashSet<long> { map }, rules, out var path) || path.Steps.Count is 0 || !path.Steps[0].IsTeleport)
        {
            Note("RouteZaapNone", map);

            return;
        }

        var outcome = await _bot.ChangeMapAsync(path.Steps[0], rules, cancellationToken).ConfigureAwait(false);

        if (outcome is not (MapChangeOutcome.Changed or MapChangeOutcome.Elsewhere))
            throw new RouteActionException(RouteActions.Zaap, outcome.ToString());
    }
}
