using D3B.Bot.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Behaviors.Nodes;

/// <summary>D3B's own nodes of session 7b: the conditions and events the character's state already reads, and the actions every behavior needs (D298).</summary>
/// <remarks>The low-level actions, walking, doors, NPCs, items, and the planning's, come at 7f (D306).</remarks>
public static class CoreNodes
{
    /// <summary>Registers them.</summary>
    /// <param name="services">The host's services.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddNode<PodsAtLeast, PercentValues>("pods.atLeast", Text("PodsAtLeast"), node => Percent(node, "PodsAtLeast"));
        services.AddNode<LifeAtLeast, PercentValues>("life.atLeast", Text("LifeAtLeast"), node => Percent(node, "LifeAtLeast"));
        services.AddNode<LevelAtLeast, LevelValues>("level.atLeast", Text("LevelAtLeast"), node =>
        {
            node.Help(Text("LevelAtLeastHelp"));
            node.Values.Number(values => values.Level, Text("Level"), 1, 200);
        });
        services.AddNode<LevelAtLeast, LevelValues>("job.levelAtLeast", Text("JobLevelAtLeast"), node =>
        {
            node.Help(Text("JobLevelAtLeastHelp")).Example(Text("JobLevelAtLeastExample"));
            node.Values.Job(values => values.Job, Text("Job")).Gathering().Required();
            node.Values.Number(values => values.Level, Text("Level"), 1, 200);
        });
        services.AddNode<AmountAtLeast, AmountValues>("kamas.atLeast", Text("KamasAtLeast"), node =>
        {
            node.Help(Text("KamasAtLeastHelp"));
            node.Values.Number(values => values.Amount, Text("Amount"), 0, 2_000_000_000);
        });
        services.AddNode<AmountAtLeast, AmountValues>("items.atLeast", Text("ItemsAtLeast"), node =>
        {
            node.Help(Text("ItemsAtLeastHelp")).Example(Text("ItemsAtLeastExample"));
            node.Values.Items(values => values.Items, Text("Items")).Required();
            node.Values.Number(values => values.Amount, Text("Amount"), 1, 2_000_000_000);
        });
        services.AddNode<InPlace, PlaceValues>("subArea.in", Text("InSubArea"), node =>
        {
            node.Help(Text("InSubAreaHelp"));
            node.Values.SubAreas(values => values.SubAreas, Text("SubAreas")).Required();
        });
        services.AddNode<InPlace, PlaceValues>("area.in", Text("InArea"), node =>
        {
            node.Help(Text("InAreaHelp"));
            node.Values.Areas(values => values.Areas, Text("Areas")).Required();
        });
        services.AddNode<LastFight, FightValues>("fight.last", Text("LastFight"), node =>
        {
            node.Help(Text("LastFightHelp"));
            node.Values.Choice(values => values.Result, Text("Result"), result => Text($"Result{result}"));
        });
        services.AddNode<RunLongerThan, DurationValues>("run.longerThan", Text("RunLongerThan"), node =>
        {
            node.Help(Text("RunLongerThanHelp"));
            node.Values.Number(values => values.Minutes, Text("Minutes"), 1, 10_080).Unit(Text("MinutesUnit"));
        });
        services.AddNode<FlagIsOn, FlagValues>("flag.on", Text("FlagOn"), node =>
        {
            node.Help(Text("FlagOnHelp"));
            node.Values.Flag(values => values.First, Text("Flag"));
        });
        services.AddNode<FlagIsOff, FlagValues>("flag.off", Text("FlagOff"), node =>
        {
            node.Help(Text("FlagOffHelp"));
            node.Values.Flag(values => values.First, Text("Flag"));
        });
        services.AddNode<BothFlags, FlagValues>("flag.and", Text("FlagAnd"), node => Pair(node, "FlagAndHelp"));
        services.AddNode<EitherFlag, FlagValues>("flag.or", Text("FlagOr"), node => Pair(node, "FlagOrHelp"));
        services.AddNode<NumberAtLeast, NumberValues>("number.atLeast", Text("NumberAtLeast"), node =>
        {
            node.Help(Text("NumberAtLeastHelp")).Example(Text("NumberAtLeastExample"));
            node.Values.Number(values => values.Value, Text("Number"), -1_000_000_000, 1_000_000_000);
            node.Values.Number(values => values.Least, Text("Least"), -1_000_000_000, 1_000_000_000);
        });

        services.AddNode<MapChanged, NoValues>("map.changed", Text("MapChanged"), node => node.Help(Text("MapChangedHelp")));
        services.AddNode<FightBegan, NoValues>("fight.began", Text("FightBegan"), node => node.Help(Text("FightBeganHelp")));
        services.AddNode<FightOver, FightValues>("fight.over", Text("FightOver"), node =>
        {
            node.Help(Text("FightOverHelp")).Example(Text("FightOverExample"));
            node.Values.Choice(values => values.Result, Text("Result"), result => Text($"Result{result}"));
        });
        services.AddNode<LevelGained, LevelValues>("level.gained", Text("LevelGained"), node => node.Help(Text("LevelGainedHelp")));
        services.AddNode<LevelGained, LevelValues>("job.levelGained", Text("JobLevelGained"), node =>
        {
            node.Help(Text("JobLevelGainedHelp"));
            node.Values.Job(values => values.Job, Text("Job")).Gathering().Required();
        });
        services.AddNode<ItemReceived, AmountValues>("item.received", Text("ItemReceived"), node =>
        {
            node.Help(Text("ItemReceivedHelp"));
            node.Values.Items(values => values.Items, Text("Items")).Help(Text("ItemReceivedItemsHelp"));
        });
        services.AddNode<Connected, NoValues>("connection.new", Text("Connected"), node => node.Help(Text("ConnectedHelp")));
        services.AddNode<Disconnected, NoValues>("connection.lost", Text("Disconnected"), node => node.Help(Text("DisconnectedHelp")));

        services.AddNode<Wait, DurationValues>("wait", Text("Wait"), node =>
        {
            node.Help(Text("WaitHelp"));
            node.Values.Number(values => values.Seconds, Text("Seconds"), 1, 3_600).Unit(Text("SecondsUnit"));
        });
        services.AddNode<WriteNote, NoteValues>("note", Text("Note"), node =>
        {
            node.Help(Text("NoteHelp")).Example(Text("NoteExample"));
            node.Values.Text(values => values.Text, Text("NoteText"), 200).Required();
        });
        services.AddNode<PauseBot, NoValues>("pause", Text("Pause"), node => node.Help(Text("PauseHelp")));
        services.AddNode<StopRun, NoValues>("stop", Text("Stop"), node => node.Help(Text("StopHelp")));
    }

    private static LocalizedText Text(string key)
    {
        return BehaviorTexts.Text(key);
    }

    private static void Percent(NodeBuilder<PercentValues> node, string key)
    {
        node.Help(Text(key + "Help"));
        node.Values.Number(values => values.Percent, Text("Percent"), 0, 100).Percent();
    }

    private static void Pair(NodeBuilder<FlagValues> node, string help)
    {
        node.Help(Text(help));
        node.Values.Flag(values => values.First, Text("First"));
        node.Values.Flag(values => values.Second, Text("Second"));
    }
}
