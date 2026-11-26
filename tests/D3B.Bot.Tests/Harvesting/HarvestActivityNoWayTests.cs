using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Journal;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>
/// A map of the zone no way leads to (D571), played on the fake game: measured on the owner's farmer of 25 and 26 November, whose
/// tours sought such maps again and again, the journey finding no way every second, while the character stood still until attacked.
/// </summary>
public sealed class HarvestActivityNoWayTests : IAsyncDisposable
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    private readonly TestPlay _play = TestPlay.WithAMapNoWayLeadsTo();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static string Say(GameText text)
    {
        return string.Format(_english, text.Format.In(_english), [.. text.Values.Select(value => value.Text ?? value.Id.ToString(_english))]);
    }

    // Every map of the zone but the shut one seen holding nothing wanted: the shut map is all the tour has to go to.
    private HarvestActivity Activity(bool restarts = false)
    {
        var sightings = new HarvestSightings();

        foreach (var map in new[] { TestPlay.NorthMapId, TestPlay.MapId, TestPlay.EastMapId, TestPlay.InsideMapId, TestPlay.OtherInsideMapId })
            sightings.SeenElements(map, [], _play.Time.GetUtcNow());

        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId], RestartAfterFailure = restarts };

        return new HarvestActivity(settings, _play.World, _play.Tables, _play.Maps, sightings, new GraphicSkills(), _play.Places, new Regrowths(), _play.Time);
    }

    [Fact]
    public async Task AMapNoWayLeadsToIsSoughtOnceAndAZoneWithNothingElseToReachEndsTheHarvestAsUnreachable()
    {
        var (_, bot) = await _play.EnterAsync(300, TestPlay.EastMapId);
        var run = bot.StartActivity(Activity());

        await _play.AdvanceUntilAsync(() => run.IsOver, TimeSpan.FromMilliseconds(500));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("No map of the zone can be reached.", Say(Assert.IsType<ActivityException>(run.Failure).Reason!));
        Assert.False(Assert.IsType<ActivityException>(run.Failure).IsFinal);
        Assert.Single(bot.Journal.All(), entry => entry is TravelEntry { Outcome: TravelOutcome.NoPath });
        Assert.DoesNotContain(_play.Log.GetSnapshot(), record => record.Message.Contains("before the next try", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AMapNoWayLeadsToIsNotSoughtAgainByTheRunsRestarts()
    {
        // The study of the owner's farmer of 26 November (D581): each restart after a failure began new tours, which sought the
        // maps no way leads to again, one journey with no way a restart.
        var (_, bot) = await _play.EnterAsync(300, TestPlay.EastMapId);
        bot.StartActivity(Activity(restarts: true));

        await _play.AdvanceUntilAsync(() => bot.Journal.All().Count(entry => entry is ActivityRestartedEntry) >= 2, TimeSpan.FromSeconds(5));

        Assert.Single(bot.Journal.All(), entry => entry is TravelEntry { Outcome: TravelOutcome.NoPath });
    }
}
