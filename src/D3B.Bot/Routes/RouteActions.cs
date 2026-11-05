using System.Collections.Immutable;
using D3B.Bot.Behaviors;
using D3B.Bot.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Routes;

/// <summary>The actions a route plays on a map, each a node of the catalog (D377).</summary>
/// <remarks>
/// <para>
/// A route's actions are nodes as a behavior's are: a kind's id, its values as a sentence of typed values, so that a
/// route's file and a behavior's name them alike and the canvas of 7e offers them too. The editor offers these.
/// </para>
/// <para>
/// Waiting plays today. The others are declared, with their values and sentences, and held by the editor; their play, which
/// asks the game for a door's use, a forced exit, a zaap, the haven bag, a harvest and a fight, is 7d3b's (D377).
/// </para>
/// </remarks>
public static class RouteActions
{
    /// <summary>The id of a harvest of a list of resources.</summary>
    public const string Harvest = "route.harvest";

    /// <summary>The id of a fight of the groups a filter lets through.</summary>
    public const string Fight = "route.fight";

    /// <summary>The id of a door taken.</summary>
    public const string Door = "route.door";

    /// <summary>The id of a forced exit.</summary>
    public const string Exit = "route.exit";

    /// <summary>The id of a wait, the behaviors' own node.</summary>
    public const string Wait = "wait";

    /// <summary>The id of a zaap taken.</summary>
    public const string Zaap = "route.zaap";

    /// <summary>The id of the haven bag entered.</summary>
    public const string HavenBag = "route.bag";

    /// <summary>The id of the node that follows a route, which a behavior plays (D379).</summary>
    public const string Follow = "route.follow";

    /// <summary>Gets the actions the editor offers, in the order of its menu.</summary>
    public static ImmutableArray<string> Offered { get; } = [Harvest, Fight, Door, Exit, Wait, Zaap, HavenBag];

    /// <summary>Tells whether a route plays an action today.</summary>
    /// <param name="node">The action's node id.</param>
    /// <returns>Whether the run plays it.</returns>
    public static bool IsPlayed(string node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node == Wait;
    }

    /// <summary>Registers the actions' nodes.</summary>
    /// <param name="services">The host's services.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddNode<FollowRoute, RouteFollowValues>(Follow, Text("RouteFollow"), node =>
        {
            node.Help(Text("RouteFollowHelp"));
            node.Values.Text(values => values.Route, Text("Route")).Required();
        });
        services.AddNode<UnplayedAction<RouteHarvestValues>, RouteHarvestValues>(Harvest, Text("RouteHarvest"), node =>
        {
            node.Help(Text("RouteHarvestHelp"));
            node.Values.Resources(values => values.Resources, Text("Resources")).Ordered().Required();
        });
        services.AddNode<UnplayedAction<RouteFightValues>, RouteFightValues>(Fight, Text("RouteFight"), node =>
        {
            node.Help(Text("RouteFightHelp"));
            node.Values.Monsters(values => values.Monsters, Text("Monsters"));
            node.Values.Number(values => values.LeastMonsters, Text("LeastMonsters"), 1, 8);
            node.Values.Number(values => values.MostMonsters, Text("MostMonsters"), 1, 8);
            node.Values.Rule(values => values.LeastMonsters <= values.MostMonsters, Text("RouteFightRule"));
        });
        services.AddNode<UnplayedAction<RouteDoorValues>, RouteDoorValues>(Door, Text("RouteDoor"), node =>
        {
            node.Help(Text("RouteDoorHelp"));
            node.Values.Cell(values => values.Cell, Text("Cell")).Required();
        });
        services.AddNode<UnplayedAction<RouteExitValues>, RouteExitValues>(Exit, Text("RouteExit"), node =>
        {
            node.Help(Text("RouteExitHelp"));
            node.Values.Choice(values => values.Direction, Text("Direction"), direction => Text($"Direction{direction}"));
            node.Values.Cell(values => values.Cell, Text("Cell"));
        });
        services.AddNode<UnplayedAction<NoValues>, NoValues>(Zaap, Text("RouteZaap"), node => node.Help(Text("RouteZaapHelp")));
        services.AddNode<UnplayedAction<NoValues>, NoValues>(HavenBag, Text("RouteBag"), node => node.Help(Text("RouteBagHelp")));
    }

    private static LocalizedText Text(string key)
    {
        return BehaviorTexts.Text(key);
    }
}
