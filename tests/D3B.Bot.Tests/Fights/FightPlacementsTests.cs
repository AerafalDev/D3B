using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Fights;

public sealed class FightPlacementsTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The learner never got there.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void EachSetOfCellsOfAMapIsCountedApart()
    {
        var placements = new FightPlacements();
        var told = new List<FightPlacement>();

        placements.Learned += told.Add;
        placements.Seen(1, [314, 300], [385, 371], _start);
        placements.Seen(1, [300, 314, 300], [371, 385], _start.AddMinutes(5));
        placements.Seen(1, [200], [201], _start.AddMinutes(9));
        placements.Seen(2, [300], [301], _start);

        var known = placements.Of(1);

        Assert.Equal(3, placements.Count);
        Assert.Equal(2, known.Count);
        Assert.Equal([300, 314], known[0].ChallengerCells);
        Assert.Equal([371, 385], known[0].DefenderCells);
        Assert.Equal(2, known[0].Seen);
        Assert.Equal(_start, known[0].FirstSeen);
        Assert.Equal(_start.AddMinutes(5), known[0].LastSeen);
        Assert.Equal(4, told.Count);
    }

    [Fact]
    public async Task EveryFightBegunTeachesItsMapsCellsOnce()
    {
        var placements = new FightPlacements();
        var time = new FakeTimeProvider(_start);

        await using var play = new TestPlay([new FightPlacementLearner(placements, time)]);
        var (connection, _) = await play.EnterAsync(433);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(new FightStartingEvent { Type = FightType.Monsters });
        connection.Server(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { ChallengerCells = { 314, 300 }, DefenderCells = { 385, 371, 999 } } });

        // The server gives the cells again as the fighters place: the same fight, counted once.
        connection.Server(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { ChallengerCells = { 314, 300 }, DefenderCells = { 385, 371 } } });
        await UntilAsync(() => placements.Count is 1);

        connection.Server(new FightStartingEvent { Type = FightType.Monsters });
        connection.Server(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { ChallengerCells = { 314, 300 }, DefenderCells = { 385, 371 } } });
        await UntilAsync(() => placements.Of(TestPlay.MapId) is [{ Seen: 2 }]);

        var placement = Assert.Single(placements.Of(TestPlay.MapId));

        Assert.Equal([300, 314], placement.ChallengerCells);
        Assert.Equal([371, 385], placement.DefenderCells);
    }
}
