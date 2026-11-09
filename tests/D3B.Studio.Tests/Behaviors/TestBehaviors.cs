using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Data.Tests;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Studio.Tests.Behaviors;

/// <summary>The behaviors' services as the module registers them, a project in a folder of a test's own, beside the bots a test plays.</summary>
internal sealed class TestBehaviors : IAsyncDisposable
{
    public const string SettingsActivityId = "test.settings";
    public const string PlainActivityId = "test.plain";

    private readonly TemporaryDirectory _directory = new();
    private readonly ServiceProvider _services;

    public TestPlay Play { get; } = new();

    public IServiceProvider Services => _services;

    public string Folder => _directory.Combine("studio");

    public AssetLibrary Assets => _services.GetRequiredService<AssetLibrary>();

    public NodeCatalog Nodes => _services.GetRequiredService<NodeCatalog>();

    public ProfileStarter Starter => _services.GetRequiredService<ProfileStarter>();

    public CharacterVariables Variables => _services.GetRequiredService<CharacterVariables>();

    public System.Collections.Concurrent.ConcurrentQueue<BotActivity> Activities { get; } = new();

    public TestBehaviors()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<TimeProvider>(Play.Time);
        services.AddSingleton(TestReferences.Game());
        var settings = SettingsDefinition();

        // Made by the behavior as it plays the node, the activities are kept for the test to read their settings.
        services.AddSingleton(new ActivityCatalog(
        [
            new ActivityDefinition(PlainActivityId, "Plain", _ => new TestActivity()),
            new ActivityDefinition(settings.Id, settings.Name, settings.Settings, (provider, values) => Made(settings.Create(provider, values))),
        ]));
        BehaviorsModule.Register(services, new StudioSettings { Path = Folder });
        _services = services.BuildServiceProvider();
    }

    public static ActivityDefinition SettingsDefinition()
    {
        var services = new ServiceCollection();

        services.AddActivity<SettingsActivity, TestSettings>(SettingsActivityId, LocalizedText.Invariant("Settings"), settings =>
        {
            settings.Number(s => s.Walks, LocalizedText.Invariant("Walks"), 1, 100);
            settings.Job(s => s.Job, LocalizedText.Invariant("Job"));
            settings.Resources(s => s.Resources, LocalizedText.Invariant("Resources")).Within(s => s.Job);
            settings.Preset("seven", LocalizedText.Invariant("Seven walks"), s => s.Walks = 7);
        });

        return services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>().Single();
    }

    public static ImmutableSortedDictionary<string, JsonNode?> Values(params (string Key, JsonNode? Value)[] values)
    {
        return values.ToImmutableSortedDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
    }

    public static JsonNode Parameter(string key)
    {
        return new JsonObject { ["$param"] = key };
    }

    public static JsonNode Variable(string name)
    {
        return new JsonObject { ["$var"] = name };
    }

    public static NodeCondition When(string node, params (string Key, JsonNode? Value)[] values)
    {
        return new NodeCondition(new NodeCall(node, Values(values)));
    }

    public static GraphNode Node(string id, string node, params (string Key, JsonNode? Value)[] values)
    {
        return new GraphNode(id, node, Values(values));
    }

    public static GraphNode Note(string id, string text)
    {
        return Node(id, "note", ("Text", text));
    }

    /// <summary>A canvas whose nodes follow one another in order, each by its way on.</summary>
    public static ActionGraph Chain(params GraphNode[] nodes)
    {
        return new ActionGraph(
            nodes.Length > 0 ? nodes[0].Id : null,
            [.. nodes],
            [.. nodes.Zip(nodes.Skip(1), (from, to) => new GraphLink(from.Id, GraphPins.Next, to.Id))]);
    }

    public static BehaviorAsset Behavior(params BehaviorState[] states)
    {
        return new BehaviorAsset { Id = AssetLibrary.NewId(), Name = "Behavior", States = [.. states] };
    }

    public static ProfileAsset ProfileOf(BehaviorAsset behavior, params (string Key, JsonNode? Value)[] values)
    {
        return new ProfileAsset { Id = AssetLibrary.NewId(), Name = "Profile", Behavior = behavior.Id, Values = Values(values) };
    }

    private BotActivity Made(BotActivity activity)
    {
        Activities.Enqueue(activity);

        return activity;
    }

    /// <summary>Reads the project, as the host does before it starts.</summary>
    public async Task<TestBehaviors> PreparedAsync()
    {
        foreach (var preparation in _services.GetServices<IHostPreparation>())
            Assert.Null(await preparation.PrepareAsync(TestContext.Current.CancellationToken));

        return this;
    }

    public BehaviorCompilation Compile(BehaviorAsset behavior, params (string Key, JsonNode? Value)[] profile)
    {
        return BehaviorCompiler.Compile(behavior, Values(profile), Nodes, _services.GetRequiredService<D3B.Bot.Settings.GameReferences>());
    }

    /// <summary>Saves a behavior in the project, then starts a profile of it on a bot.</summary>
    public async Task<ActivityRun> StartAsync(BotActor bot, BehaviorAsset behavior, params (string Key, JsonNode? Value)[] profile)
    {
        await Assets.SaveAsync(behavior, TestContext.Current.CancellationToken);

        var start = Starter.Start(bot, ProfileOf(behavior, profile));

        Assert.Null(start.Failure);
        Assert.Empty(start.Problems);

        return start.Run!;
    }

    public static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold in time.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The notes the bot's behaviors wrote, in order.</summary>
    public static IReadOnlyList<string> Notes(BotActor bot)
    {
        return [.. bot.Journal.All().OfType<D3B.Bot.Journal.NoteEntry>().Select(note => note.Text)];
    }

    /// <summary>The states the bot's behaviors entered, in order.</summary>
    public static IReadOnlyList<string?> States(BotActor bot)
    {
        return [.. bot.Journal.All().OfType<D3B.Bot.Journal.StateEnteredEntry>().Select(entry => entry.State)];
    }

    public async ValueTask DisposeAsync()
    {
        await Play.DisposeAsync();
        await _services.DisposeAsync();
        _directory.Dispose();
    }
}
