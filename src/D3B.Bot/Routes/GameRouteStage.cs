using System.Globalization;
using D3B.Bot.Behaviors;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;

namespace D3B.Bot.Routes;

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
    public Task PlayAsync(NodeCall action, CancellationToken cancellationToken)
    {
        if (!_nodes.TryGet(action.Node, out var definition))
            throw new InvalidOperationException($"The route holds the action {action.Node}, which the host does not know.");

        var values = RouteActionCalls.Read(definition, action).Settings ?? new object();

        return ((IActionNode)_nodes.CodeOf(definition)).RunAsync(_context, values, cancellationToken);
    }

    /// <inheritdoc/>
    public Task WaitAsync(TimeSpan time, CancellationToken cancellationToken)
    {
        return Task.Delay(time, _bot.Time, cancellationToken);
    }

    /// <inheritdoc/>
    public void Note(string key, params object[] values)
    {
        var culture = CultureInfo.CurrentUICulture;

        _context.Note(string.Format(culture, BehaviorTexts.Resources.GetString(key, culture) ?? key, values));
    }
}
