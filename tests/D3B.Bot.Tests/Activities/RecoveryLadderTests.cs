using D3B.Bot.Activities;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Activities;

/// <summary>The recovery ladder (D518): what an activity does, rung after rung, before it tries an action that failed again.</summary>
public sealed class RecoveryLadderTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheLadderTriesTwiceThenPutsInOrderStepsAsideAndLeavesTheMapThenHasNoRungLeft()
    {
        // On the north scroll's own cell, which the map is left from with no walk.
        var (connection, bot) = await _play.EnterAsync(6);
        var ladder = new RecoveryLadder();
        var climbing = bot.RunAsync(
            async (context, token) =>
            {
                var rungs = new List<RecoveryRung?>();

                for (var rung = 0; rung <= RecoveryLadder.Height; rung++)
                    rungs.Add(await ladder.ClimbAsync(context, "the test's action", token));

                return rungs;
            },
            TestContext.Current.CancellationToken);

        var rungs = await TestWalks.AdvanceUntilDoneAsync(_play.Time, climbing);

        Assert.Equal([RecoveryRung.Retry, RecoveryRung.Retry, RecoveryRung.PutInOrder, RecoveryRung.StepAside, RecoveryRung.LeaveMap, null], rungs);
        Assert.Equal(RecoveryLadder.Height, ladder.Climbed);

        // Nothing to put in order; the step aside, then the scroll north, both unanswered.
        Assert.Collection(
            connection.Sent,
            request => Assert.IsType<MapMovementRequest>(request),
            request => Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, request));

        ladder.Reset();

        Assert.Equal(0, ladder.Climbed);
    }

    [Fact]
    public async Task AnActionIsTriedUntilItSucceedsAndTheLadderIsBackAtItsFoot()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var ladder = new RecoveryLadder();
        var tries = 0;
        var outcome = await bot.RunAsync((context, token) => ladder.TryAsync(context, "the test's action", _ => Task.FromResult(++tries), tried => tried is 3, token), TestContext.Current.CancellationToken);

        Assert.Equal((3, 3, 0), (outcome, tries, ladder.Climbed));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnActionThatAlwaysFailsIsTriedOnceAndOnceMoreEachRung()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var ladder = new RecoveryLadder();
        var tries = 0;
        var trying = bot.RunAsync((context, token) => ladder.TryAsync(context, "the test's action", _ => Task.FromResult(++tries), _ => false, token), TestContext.Current.CancellationToken);

        Assert.Equal(RecoveryLadder.Height + 1, await TestWalks.AdvanceUntilDoneAsync(_play.Time, trying));
        Assert.Contains(_play.Log.GetSnapshot(), record => record.Message.Contains("the test's action failed; before the next try, rung 5 of 5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheMapIsLeftFromTheCellOfItsNearestExitWithNoWalk()
    {
        var (connection, bot) = await _play.EnterAsync(6);
        var leaving = bot.RunAsync((context, token) => context.LeaveByNearestExitAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestWalks.SentAsync(_play, connection, 1));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 539);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(_play.Time, leaving));
    }

    [Fact]
    public async Task AnInsideIsLeftByItsOnlyWayOutAMapAction()
    {
        var (connection, bot) = await _play.EnterAsync(300, TestPlay.OtherInsideMapId);
        var leaving = bot.RunAsync((context, token) => context.LeaveByNearestExitAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal(TestPlay.OtherActionCell, await TestWalks.WalkedAsync(_play, connection, 1, new MapCurrentEvent { MapId = TestPlay.MapId }));

        connection.Client(new MapInformationRequest { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(TestPlay.DoorCell, TestPlay.MapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(_play.Time, leaving));
    }
}
