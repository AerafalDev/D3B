using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>
/// The measured ways a harvest was left idle until the server closed the connection (D481, D484, D488, D517), played on the fake
/// game: each now ends with the character still acting, by the recovery ladder (D518) or the run's restart (D519).
/// </summary>
public sealed class HarvestNeverIdleTests : IAsyncDisposable
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    // Measured (D481, D484): the server closes the connection 30 minutes after the bot's last request.
    private static readonly TimeSpan _kick = TimeSpan.FromMinutes(30);

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static string Say(GameText text)
    {
        return string.Format(_english, text.Format.In(_english), [.. text.Values.Select(value => value.Text ?? value.Id.ToString(_english))]);
    }

    private HarvestActivity Activity()
    {
        return new HarvestActivity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, _play.World, _play.Tables, _play.Maps, new HarvestSightings(), new GraphicSkills(), _play.Places, new Regrowths(), _play.Time);
    }

    // As the owner's miner was at 03:32:27 into the capture of 24 November: 90.1 % of its pods, in a mine whose only way out is one
    // way, here the other inside's map action.
    private async Task<(TestConnection Connection, BotActor Bot)> FullInTheMineAsync()
    {
        var (connection, bot) = await _play.EnterAsync(300, TestPlay.OtherInsideMapId);

        connection.Server(new InventoryWeightEvent { Weight = 901, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    // Moves the clock until the run is over or the time passed, noting when each request of the bot went.
    private async Task<List<DateTimeOffset>> RequestsUntilAsync(TestConnection connection, ActivityRun run, TimeSpan time, TimeSpan tick)
    {
        var end = _play.Time.GetUtcNow() + time;
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        var sent = new List<DateTimeOffset>();
        var seen = connection.Sent.Count;

        while (!run.IsOver && _play.Time.GetUtcNow() < end)
        {
            Assert.True(DateTime.UtcNow < deadline, "The clock did not get there in time.");
            _play.Time.Advance(tick);
            await Task.Delay(5, TestContext.Current.CancellationToken);

            var count = connection.Sent.Count;

            for (; seen < count; seen++)
                sent.Add(_play.Time.GetUtcNow());
        }

        return sent;
    }

    [Fact]
    public async Task TheMinersBankJourneyWhoseWalkOutOfTheMineWasRefusedWithItsConfirmationIsTriedAgainAndBanks()
    {
        // Measured on the capture of 24 November (D517): the walk to the mine's door shown at 03:32:26, then its refusal and the
        // confirmation's response in one batch; the door used and refused, the bank's journey found no way, the unloading gave up,
        // the harvest failed, and nothing was sent until the server closed the connection 30 minutes later.
        var (connection, bot) = await FullInTheMineAsync();
        var run = bot.StartActivity(Activity());
        var request = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(_play, connection, 1));

        Assert.True(CellPathfinder.TryFind(_play.Maps.Get(request.MapId).Cells.Span, 300, TestPlay.OtherActionCell, out var path));

        var stood = MapGeometry.PointOf(path.Cells.Span[1]);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });
        connection.Response(new MapMovementConfirmResponse());

        // The banking tried again, from where the server holds the character, the walk it holds let go by a step aside first (D456):
        // out of the mine, through the bank's door, to the banker.
        var count = 2;

        while (Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(_play, connection, count)).KeyMovements[^1] % 4096 != TestPlay.OtherActionCell)
            await TestWalks.WalkedAsync(_play, connection, count++);

        await TestWalks.WalkedAsync(_play, connection, count++, new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Client(new MapInformationRequest { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(TestPlay.OtherActionCell, TestPlay.MapId));

        await TestWalks.WalkedAsync(_play, connection, count++);

        Assert.Equal(new InteractiveUseRequest { ElementId = TestPlay.DoorElementId, SkillInstanceId = TestPlay.DoorInstanceId }, await TestWalks.SentAsync(_play, connection, count++));

        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, count));
        Assert.False(run.IsOver);
        Assert.Contains(_play.Log.GetSnapshot(), record => record.Message.Contains("the bank failed; before the next try, rung 1", StringComparison.Ordinal));
        Assert.Equal(0, run.Restarts);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AnUnloadingThatNeverReachesABankRestartsTheHarvestRatherThanLeaveTheCharacterIdle()
    {
        // The server never shows a walk out of the mine: every try of the ladder fails, and the run begins the harvest anew.
        var (connection, bot) = await FullInTheMineAsync();
        var run = bot.StartActivity(Activity());

        await _play.AdvanceUntilAsync(() => run.LastRestart is not null, TimeSpan.FromMilliseconds(500));

        Assert.Equal("The character could not be unloaded, even once put back in order and out of its map.", Say(Assert.IsType<ActivityException>(run.LastRestart!.Cause).Reason!));

        var before = connection.Sent.Count;

        await _play.AdvanceUntilAsync(() => connection.Sent.Count > before, TimeSpan.FromSeconds(1));

        Assert.False(run.IsOver);
        Assert.IsType<MapMovementRequest>(connection.Sent[^1]);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AUseThatNeverEndsRestartsTheHarvestOnceItFailedSayingWhy()
    {
        // D484's way: a use whose end never comes, put back in order after a minute, then failing a minute later; the run puts the
        // character in order again and begins the harvest anew, rather than leave it until the server's kick.
        var (connection, bot) = await _play.EnterAsync(433);

        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        await bot.ProcessedAsync();

        var run = bot.StartActivity(Activity());

        await _play.AdvanceUntilAsync(() => run.LastRestart is not null, TimeSpan.FromSeconds(1));

        Assert.Equal("Still using an element after two minutes, even once put back in order.", Say(Assert.IsType<ActivityException>(run.LastRestart!.Cause).Reason!));
        Assert.False(run.IsOver);

        // The use ends at last: the harvest begun anew walks to the tree.
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(_play, connection, 1));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AHarvestWhoseEveryStepFailsIsNeverIdleTillTheServersKickThenEndsSayingItGaveUp()
    {
        // The server answers nothing: every walk, use and change fails, and with them every journey, the ladder's rungs and the restarts.
        var (connection, bot) = await _play.EnterAsync(300);
        var run = bot.StartActivity(Activity());
        var sent = await RequestsUntilAsync(connection, run, TimeSpan.FromHours(2), TimeSpan.FromSeconds(2));

        Assert.True(run.IsOver, "The harvest still runs after two hours of failures.");
        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.True(run.GaveUp);
        Assert.Equal(ActivityRestarts.Default.MostPerHour, run.LastRestart!.Restart);
        Assert.NotNull(Assert.IsType<ActivityException>(run.Failure).Reason);

        var times = sent.Prepend(run.StartedAt).Append(run.EndedAt!.Value).ToList();
        var longest = times.Zip(times.Skip(1), (before, after) => after - before).Max();

        // From the run's start to its end, the longest the bot went without a request: the longest wait before a restart, and a
        // rung's own delays, far from the server's 30 minutes.
        Assert.True(longest < ActivityRestarts.Default.LongestDelay + TimeSpan.FromMinutes(1), $"The bot went {longest} without a request.");
        Assert.True(longest < _kick);
    }
}
