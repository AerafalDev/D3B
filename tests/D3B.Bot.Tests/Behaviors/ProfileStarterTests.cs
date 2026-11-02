using D3B.Bot.Activities;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Settings;
using static D3B.Bot.Tests.Behaviors.TestBehaviors;

namespace D3B.Bot.Tests.Behaviors;

public sealed class ProfileStarterTests : IAsyncDisposable
{
    private readonly TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    private async Task<BehaviorAsset> GivenAsync()
    {
        await _behaviors.PreparedAsync();

        return _behaviors.Assets.Find<BehaviorAsset>(GivenAssets.BehaviorOf(SettingsActivityId))!;
    }

    [Fact]
    public async Task AProfileStartsItsBehaviorUnderItsName()
    {
        var behavior = await GivenAsync();
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var start = _behaviors.Starter.Start(bot, ProfileOf(behavior, ("Walks", 7), ("Job", TestReferences.Lumberjack), ("Resources", new System.Text.Json.Nodes.JsonArray(TestReferences.Chestnut))));

        Assert.Null(start.Failure);
        Assert.Same(bot.Activity, start.Run);
        Assert.Equal(("Profile", "Profile"), (start.Run!.Title, start.Run.Activity.Name));
        Assert.Equal(ActivityOutcome.Done, await start.Run.Completion);

        var activity = Assert.IsType<SettingsActivity>(Assert.Single(_behaviors.Activities));

        Assert.Equal((7, (int?)TestReferences.Lumberjack), (activity.Settings.Walks, activity.Settings.Job));
        Assert.Equal([TestReferences.Chestnut], activity.Settings.Resources);
    }

    [Fact]
    public async Task AProfileWhoseBehaviorIsGoneDoesNotStart()
    {
        await _behaviors.PreparedAsync();

        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var start = _behaviors.Starter.Start(bot, new ProfileAsset { Id = "p", Name = "P", Behavior = "gone" });

        Assert.Equal(ProfileStartFailure.BehaviorMissing, start.Failure);
        Assert.Null(bot.Activity);
        Assert.Equal(ProfileStartFailure.ProfileMissing, _behaviors.Starter.Start(bot, "nothing").Failure);
    }

    [Fact]
    public async Task AProfileValueThatNoLongerReadsIsLookedAtFirst()
    {
        var behavior = await GivenAsync();
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var start = _behaviors.Starter.Start(bot, ProfileOf(behavior, ("Walks", "many")));

        Assert.Equal(ProfileStartFailure.Invalid, start.Failure);
        Assert.Equal([BehaviorProblemKind.ProfileValueDoesNotRead], start.Problems.Select(problem => problem.Kind));
        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task AProfileWhoseValuesDoNotHoldInThePackDoesNotStart()
    {
        var behavior = await GivenAsync();
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var start = _behaviors.Starter.Start(bot, ProfileOf(behavior, ("Job", TestReferences.Miner), ("Resources", new System.Text.Json.Nodes.JsonArray(TestReferences.Ash))));

        Assert.Equal(ProfileStartFailure.Invalid, start.Failure);
        Assert.Equal([(BehaviorProblemKind.ValueInvalid, "main/activity", "Resources: NotWithin")], start.Problems.Select(problem => (problem.Kind, problem.Where, problem.Detail)));
    }

    [Fact]
    public async Task ABotAlreadyRunningAnActivityDoesNotStartAProfile()
    {
        var behavior = await GivenAsync();
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var running = bot.StartActivity(new TestActivity());
        var start = _behaviors.Starter.Start(bot, ProfileOf(behavior));

        Assert.Equal(ProfileStartFailure.NotStarted, start.Failure);
        Assert.Equal("The bot already runs an activity.", start.Error);
        Assert.Same(running, bot.Activity);
        running.Stop();
    }
}
