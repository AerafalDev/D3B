using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Harvesting;

/// <summary>The harvest's tour coming onto a part of a map its own part cannot walk to, such as a mine's ledge, from a neighbouring map (D495).</summary>
public sealed class HarvestActivityLevelsTests
{
    // The ledge: a strip from cell 6 of the top edge down to the tree, which stands on it, cut by its cliff from the rest of the map.
    private static int[] Ledge()
    {
        var open = Enumerable.Repeat(new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 1 << 4, 0), MapData.CellCount).ToArray();

        Assert.True(CellPathfinder.TryFind(open, 6, TestPlay.TreeCell, out var path));

        return path.Cells.ToArray();
    }

    // The tour goes through the first map only: the others were seen holding nothing.
    private static HarvestActivity ActivityOf(TestPlay play)
    {
        var sightings = new HarvestSightings();

        sightings.Seen(TestPlay.NorthMapId, [], DateTimeOffset.UnixEpoch);
        sightings.Seen(TestPlay.EastMapId, [], DateTimeOffset.UnixEpoch);

        return new HarvestActivity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }, play.World, play.Tables, play.Maps, sightings, new GraphicSkills(), play.Places, new Regrowths(), play.Time);
    }

    [Fact]
    public async Task ATourComesBackOntoTheMapFromTheSideItsLedgeTouchesAndHarvestsTheResourceBelowTheCliff()
    {
        // Measured on the owner's miner: the ore's neighbours on its floor were cut from the character's, and the server refused its
        // use from above; the ledge was entered only from the map on its side, a map away.
        var ledge = Ledge();
        await using var play = TestPlay.WithALedge(ledge);
        var (connection, bot) = await play.EnterAsync(433);
        var run = bot.StartActivity(ActivityOf(play));

        // Nothing is used from above the cliff: the map is gone through, and the way to the ledge leaves north, by the floor 0.
        var exit = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.DoesNotContain(exit, ledge);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestWalks.SentAsync(play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, exit + 532);

        // From the north map, the scroll back leaves from a cell straight above the ledge's cells of the top edge.
        var across = await TestWalks.WalkedAsync(play, connection, 3);

        Assert.Contains(across - 532, ledge);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 4));
        TestWalks.Arrive(connection, TestPlay.MapId, across - 532);

        // Down the ledge to the tree's neighbour on its floor, then the harvest.
        var below = await TestWalks.WalkedAsync(play, connection, 5);

        Assert.Contains(below, ledge);
        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await TestWalks.SentAsync(play, connection, 6));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }
}
