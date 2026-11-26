using System.Reflection;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using static D3B.Studio.Tests.Behaviors.TestBehaviors;

namespace D3B.Studio.Tests.Behaviors;

/// <summary>
/// A profile's activity, played by the behavior, is restarted as its own run would restart it (D519). Measured on the owner's four
/// harvesters of 25 November: each profile's harvest failed its unloading and was never restarted, the behavior answering for the
/// activity it played that it would not be, and each character idled until the server closed its connection 30 minutes later.
/// </summary>
public sealed class BehaviorActivityRestartTests : IAsyncDisposable
{
    private readonly TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    private async Task<ActivityRun> StartedAsync(string activityId)
    {
        await _behaviors.PreparedAsync();

        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = _behaviors.Assets.Find<BehaviorAsset>(GivenAssets.BehaviorOf(activityId))!;
        var start = _behaviors.Starter.Start(bot, ProfileOf(behavior));

        Assert.Null(start.Failure);

        return start.Run!;
    }

    [Fact]
    public async Task AProfilesActivityThatFailsIsRestartedRatherThanLeftIdle()
    {
        var run = await StartedAsync(FailingActivityId);

        await _behaviors.Play.AdvanceUntilAsync(() => run.IsOver || _behaviors.Activities.OfType<FailingActivity>().Any(activity => activity.Restarted > 0), TimeSpan.FromSeconds(1));

        var activity = Assert.Single(_behaviors.Activities.OfType<FailingActivity>());

        Assert.False(run.IsOver);
        Assert.Equal(1, activity.Restarted);
        Assert.Equal("The step failed.", run.LastRestart!.Cause.Message);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AProfilesActivityThatMakesNoProgressIsRestartedThoughTheBehaviorPlayedNoneAsTheRunBegan()
    {
        var run = await StartedAsync(StuckActivityId);

        await _behaviors.Play.AdvanceUntilAsync(() => run.IsOver || run.LastRestart is not null, TimeSpan.FromSeconds(15));

        Assert.False(run.IsOver);
        Assert.StartsWith("Nothing done for 10 minutes", run.LastRestart!.Cause.Message, StringComparison.Ordinal);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AProfilesRunSaysWhatItsActivitySaysOfItself()
    {
        // Players' reports of 26 November: a profile's master in a dungeon whose follower stayed at its door, finding no way to it,
        // and a profile capturing Volkornes that never cast its net; the followers read the dungeon, and the fight the races, of
        // the run's activity (D528, D533).
        var run = await StartedAsync(SpeakingActivityId);

        await UntilAsync(() => run.Status is ActivityStatus.Stepping);

        var activity = run.Activity;
        var stack = new D3B.Bot.Characters.InventoryItem(1, 303, 1, D3B.Bot.Characters.InventoryItem.NotWorn);

        Assert.Equal([SpeakingActivity.Race], activity.MountRaces);
        Assert.Equal(SpeakingActivity.Dungeon, activity.DungeonInside);
        Assert.Equal(SpeakingActivity.Until, activity.WaitingUntil);
        Assert.True(activity.CapturesSouls);
        Assert.True(activity.FightsAsMule);
        Assert.True(activity.RestartsAfterFailure);
        Assert.True(activity.BanksTheGroup);
        Assert.True(activity.Keeps(stack));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public void TheBehaviorSpeaksForItsActivityInEveryWayAnActivityCanSaySomething()
    {
        // Each member an activity may override is read of the run's activity, which a profile's is the behavior: one it does not pass
        // on is lost for every profile, as the restart was, and a soul's capture (D565), a mount's (D533) and a dungeon's keys (D529).
        // A member added to BotActivity fails this until the behavior overrides it, which is where it is decided whether it passes
        // on: all do but the name and the outline, the behavior's own, and the watch, which plays the behavior's rules besides.
        var virtuals = typeof(BotActivity).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                MethodInfo method => method.IsVirtual && !method.IsFinal && !method.IsSpecialName,
                PropertyInfo property => property.GetMethod is { IsVirtual: true },
                _ => false,
            })
            .ToList();

        Assert.NotEmpty(virtuals);

        var missing = virtuals.Where(member => member switch
        {
            MethodInfo method => typeof(BehaviorActivity).GetMethod(method.Name, [.. method.GetParameters().Select(parameter => parameter.ParameterType)])!.DeclaringType != typeof(BehaviorActivity),
            PropertyInfo property => typeof(BehaviorActivity).GetProperty(property.Name)!.GetMethod!.DeclaringType != typeof(BehaviorActivity),
            _ => false,
        }).Select(member => member.Name);

        Assert.Empty(missing);
    }
}
