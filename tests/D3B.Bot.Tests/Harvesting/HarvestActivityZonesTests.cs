using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>The harvest's tour going to the other zone of a map, where a wanted resource stands out of reach of the zone it harvested in, and back (D425).</summary>
public sealed class HarvestActivityZonesTests
{
    // The tree's neighbours are the second zone's: the character, in the first, can use it from none (D424).
    private static int[] Ring()
    {
        return [.. Enumerable.Range(0, 8).Select(direction => MapGeometry.Neighbour(TestPlay.TreeCell, direction))];
    }

    // The tour goes through the first map only: the others were seen holding nothing.
    private static HarvestActivity ActivityOf(TestPlay play, HarvestSightings sightings)
    {
        sightings.Seen(TestPlay.NorthMapId, [], DateTimeOffset.UnixEpoch);
        sightings.Seen(TestPlay.EastMapId, [], DateTimeOffset.UnixEpoch);

        return new HarvestActivity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, play.World, play.Tables, play.Maps, sightings, new GraphicSkills(), play.Places, new Regrowths(), play.Time);
    }

    [Fact]
    public async Task ATourGoesOutAndBackIntoTheOtherZoneForAResourceStandingThereAndHarvestsIt()
    {
        await using var play = TestPlay.WithASecondZone(Ring());
        var (connection, bot) = await play.EnterAsync(433);
        var run = bot.StartActivity(ActivityOf(play, new HarvestSightings()));

        // Nothing is used from afar: the first map is gone through, and the way to the other zone leaves it by the east, then comes back.
        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(TestPlay.EastOtherZoneActionCell, await TestWalks.WalkedAsync(play, connection, 2, new MapCurrentEvent { MapId = TestPlay.MapId }));
        connection.Server(TestPlay.Information(Ring()[1]));

        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await TestWalks.SentAsync(play, connection, 3));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AZoneNoWayLeadsToIsLeftAloneAndTheTourGoesOn()
    {
        await using var play = TestPlay.WithASecondZoneNoWayLeadsTo(Ring());
        var (connection, bot) = await play.EnterAsync(433);
        var run = bot.StartActivity(ActivityOf(play, new HarvestSightings()));

        while (!play.Log.GetSnapshot().Any(record => record.Level is LogLevel.Debug && record.Message.Contains("leaves zone 2 of map", StringComparison.Ordinal)))
        {
            play.Time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Empty(connection.Sent);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }
}
