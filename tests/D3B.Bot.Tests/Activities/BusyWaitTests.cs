using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Activities;

/// <summary>The bounded wait for a busy character at a step's start (D484): what it names, and when its count starts again.</summary>
public sealed class BusyWaitTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly BusyWait _busy = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static ActivityException Failure(string message, string key)
    {
        return new ActivityException($"{key}: {message}");
    }

    private static CharacterState State(long? mapId = null, CurrentUse? use = null, NpcDialog? dialog = null, params int[] path)
    {
        return new CharacterState { Key = TestPlay.Key, Name = "Character", MapId = mapId, Use = use, Dialog = dialog, Path = [.. path] };
    }

    // Steps as an activity's run does, one after another, until the clock has passed the time given.
    private async Task StepForAsync(BotActor bot, TimeSpan time)
    {
        var end = _play.Time.GetUtcNow() + time;

        while (_play.Time.GetUtcNow() < end)
        {
            var step = bot.RunAsync((context, token) => _busy.WaitAsync(context, Failure, token), TestContext.Current.CancellationToken);

            while (!step.IsCompleted)
            {
                _play.Time.Advance(TimeSpan.FromSeconds(1));
                await Task.Delay(5, TestContext.Current.CancellationToken);
            }

            await step;
        }
    }

    [Fact]
    public void TheMapUnknownIsNamedFirstThenTheUseTheDialogAndTheWalk()
    {
        Assert.Equal(BusyKind.MapUnknown, BusyWait.Of(State(null, new CurrentUse(1, 2), new NpcDialog(3), 4, 5)));
        Assert.Equal(BusyKind.Using, BusyWait.Of(State(1, new CurrentUse(1, 2), new NpcDialog(3), 4, 5)));
        Assert.Equal(BusyKind.InDialog, BusyWait.Of(State(1, null, new NpcDialog(3), 4, 5)));
        Assert.Equal(BusyKind.Walking, BusyWait.Of(State(1, null, null, 4, 5)));
        Assert.Null(BusyWait.Of(State(1)));
    }

    [Fact]
    public async Task ACharacterBusyAgainAfterAStepFoundItFreeIsCountedFromThenOn()
    {
        var (connection, bot) = await _play.EnterAsync(433);

        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        await StepForAsync(bot, TimeSpan.FromSeconds(50));

        connection.Server(new DialogLeaveEvent());
        await bot.ProcessedAsync();
        Assert.False(await bot.RunAsync((context, token) => _busy.WaitAsync(context, Failure, token), TestContext.Current.CancellationToken));

        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        await StepForAsync(bot, TimeSpan.FromSeconds(50));

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task StepsThatStopAWhileAsThePausesBetweenTwoMakeThemStartTheCountAgain()
    {
        var (connection, bot) = await _play.EnterAsync(433);

        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        await StepForAsync(bot, TimeSpan.FromSeconds(50));

        _play.Time.Advance(TimeSpan.FromMinutes(5));
        await StepForAsync(bot, TimeSpan.FromSeconds(50));

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterBusyAMinuteIsPutBackInOrder()
    {
        var (connection, bot) = await _play.EnterAsync(433);

        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        await StepForAsync(bot, TimeSpan.FromSeconds(70));

        Assert.Equal(new DialogLeaveRequest(), Assert.Single(connection.Sent));
    }
}
