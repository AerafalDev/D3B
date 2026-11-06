using D3B.Bot.Behaviors;
using D3B.Bot.Harvesting;
using D3B.Data;

namespace D3B.Bot.Routes;

/// <summary>Harvests the resources of a list on the map the route stands on, then goes on (D384).</summary>
internal sealed class HarvestHere : ActionNode<RouteHarvestValues>
{
    private readonly GameTables _tables;

    /// <summary>Initializes a new instance of the <see cref="HarvestHere"/> class.</summary>
    /// <param name="tables">The installed build's tables: the professions' skills.</param>
    public HarvestHere(GameTables tables)
    {
        _tables = tables;
    }

    public override async Task RunAsync(ActionContext context, RouteHarvestValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("A harvest is played in a step, which has a bot to move.");
        var targets = HarvestZone.TargetsOf(new HarvestSettings { Mode = HarvestMode.List, Resources = values.Resources }, _tables.Jobs);

        await MapHarvest.HarvestAsync(bot, targets, cancellationToken).ConfigureAwait(false);
    }
}
