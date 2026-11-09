using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using D3B.Studio.Behaviors;
using D3B.Studio.Behaviors.Nodes;
using Microsoft.Extensions.DependencyInjection;
using FightResult = D3B.Studio.Behaviors.Nodes.FightResult;

namespace D3B.Studio.Tests.Behaviors;

public sealed class CoreNodesTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static CharacterState State(Func<CharacterState, CharacterState>? change = null)
    {
        var state = new CharacterState { Key = TestPlay.Key, Name = "Character", Level = 50, MapId = TestPlay.MapId, SubAreaId = TestPlay.SubAreaId };

        return change?.Invoke(state) ?? state;
    }

    private static BehaviorView View(CharacterState state, bool connected = true, int fightsEnded = 0, FightEnd? end = null)
    {
        return new BehaviorView(state, connected, fightsEnded, end, _now, _now - TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void ThePodsAndTheLifeAreReadAsShares()
    {
        var pods = new PodsAtLeast();
        var life = new LifeAtLeast();

        Assert.True(pods.Holds(View(State(state => state with { Weight = 900, MaxWeight = 1000 })), new PercentValues { Percent = 90 }));
        Assert.False(pods.Holds(View(State(state => state with { Weight = 899, MaxWeight = 1000 })), new PercentValues { Percent = 90 }));
        Assert.False(pods.Holds(View(State()), new PercentValues { Percent = 0 }));
        Assert.True(life.Holds(View(State(state => state with { Life = 50, MaxLife = 100 })), new PercentValues { Percent = 50 }));
        Assert.False(life.Holds(View(State()), new PercentValues { Percent = 0 }));
    }

    [Fact]
    public void TheLevelsTheItemsAndThePlaceAreRead()
    {
        var job = new CharacterJob(TestPlay.Lumberjack, 40, 0, 0, 0, []);
        var state = State(state => state with
        {
            Jobs = ImmutableSortedDictionary<int, CharacterJob>.Empty.Add(TestPlay.Lumberjack, job),
            Inventory = ImmutableSortedDictionary<int, InventoryItem>.Empty.Add(1, new(1, TestPlay.Ash, 60, InventoryItem.NotWorn)).Add(2, new(2, TestPlay.Ash, 40, InventoryItem.NotWorn)),
            Kamas = 500,
        });

        Assert.True(new LevelAtLeast().Holds(View(state), new LevelValues { Level = 50 }));
        Assert.True(new LevelAtLeast().Holds(View(state), new LevelValues { Job = TestPlay.Lumberjack, Level = 40 }));
        Assert.False(new LevelAtLeast().Holds(View(state), new LevelValues { Job = 24, Level = 1 }));
        Assert.True(new AmountAtLeast().Holds(View(state), new AmountValues { Items = [TestPlay.Ash], Amount = 100 }));
        Assert.True(new AmountAtLeast().Holds(View(state), new AmountValues { Amount = 500 }));
        Assert.True(new InPlace(new ServiceCollection().BuildServiceProvider()).Holds(View(state), new PlaceValues { SubAreas = [TestPlay.SubAreaId] }));
        Assert.True(new RunLongerThan().Holds(View(state), new DurationValues { Minutes = 30 }));
    }

    [Fact]
    public void TheLastFightIsReadAsWonOrLost()
    {
        var lost = new FightEnd(FightOutcome.Lost, 0, 0, ImmutableSortedDictionary<int, int>.Empty);

        Assert.True(new LastFight().Holds(View(State(), end: lost), new FightValues { Result = FightResult.Lost }));
        Assert.False(new LastFight().Holds(View(State(), end: lost), new FightValues { Result = FightResult.Won }));
        Assert.False(new LastFight().Holds(View(State()), new FightValues { Result = FightResult.Any }));
        Assert.True(new FightOver().Happened(new BotChange(View(State()), View(State(), fightsEnded: 1, end: lost)), new FightValues { Result = FightResult.Any }));
    }

    [Fact]
    public void EventsReadWhatChangedOnTheSameConnection()
    {
        var before = View(State());
        var levelled = View(State(state => state with { Level = 51 }));
        var moved = View(State(state => state with { MapId = TestPlay.NorthMapId }));
        var received = View(State(state => state with { Inventory = state.Inventory.Add(1, new(1, TestPlay.Ash, 1, InventoryItem.NotWorn)) }));

        Assert.True(new LevelGained().Happened(new BotChange(before, levelled), new LevelValues()));
        Assert.False(new LevelGained().Happened(new BotChange(View(State(), connected: false), levelled), new LevelValues()));
        Assert.True(new MapChanged().Happened(new BotChange(before, moved), new NoValues()));
        Assert.True(new ItemReceived().Happened(new BotChange(before, received), new AmountValues { Items = [TestPlay.Ash] }));
        Assert.False(new ItemReceived().Happened(new BotChange(before, received), new AmountValues { Items = [TestPlay.Chestnut] }));
        Assert.True(new Connected().Happened(new BotChange(View(State(), connected: false), before), new NoValues()));
        Assert.True(new Disconnected().Happened(new BotChange(before, View(State(), connected: false)), new NoValues()));
    }
}
