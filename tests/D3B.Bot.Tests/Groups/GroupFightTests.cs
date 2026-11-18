using D3B.Bot.Activities;
using D3B.Bot.Groups;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Groups;

/// <summary>The group's fights: a follower joining its master's by request, the master leaving it to its party and ready once its followers are placed (D431, D434).</summary>
public sealed class GroupFightTests
{
    private const long Monster = -1;
    private const int FightId = 7;
    private const int Far = 300;

    private static int Beside(int cell, int dx, int dy)
    {
        var point = MapGeometry.PointOf(cell);

        Assert.True(MapGeometry.TryGetCell(new MapPoint(point.X + dx, point.Y + dy), out var found));

        return found;
    }

    private static Characteristic Usable(int id, int left)
    {
        return new Characteristic { Id = id, Usable = new UsableCharacteristic { Number4 = left } };
    }

    private static Characteristic Detailed(int id, int value)
    {
        return new Characteristic { Id = id, Detailed = new DetailedCharacteristic { Number7 = value } };
    }

    private static Characteristic Value(int id, long total)
    {
        return new Characteristic { Id = id, Value = new CharacteristicValue { Total = total } };
    }

    private static Actor Character(long id, int cell)
    {
        return new Actor
        {
            Id = id,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Named = new NamedFighter { Character = new FighterCharacter { BreedId = 15, Level = 18 } },
                    Stats = new FightCharacteristics { Characteristics = { Usable(1, 6), Usable(23, 0), Detailed(0, 140), Detailed(11, 58) } },
                    Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Challenger },
                },
            },
        };
    }

    private static Actor MonsterActor(int cell)
    {
        return new Actor
        {
            Id = Monster,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Ai = new AiFighter { Monster = new MonsterFighter { MonsterId = 31, Grade = 1, Level = 3 } },
                    Stats = new FightCharacteristics { Characteristics = { Value(1, 2), Value(23, 3), Value(0, 50) } },
                    Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Defender },
                },
            },
        };
    }

    // The placement as the server shows it to one of the team, the master's attack (D435): its cell and another of its team's.
    private static void Placement(TestConnection connection, long self, int cell, int near, int monster)
    {
        var order = new FightTurnListEvent();

        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = self } });
        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = Monster } });

        foreach (var message in new IMessage[]
        {
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = TestPlay.MapId },
            new FightStartingEvent { AttackerId = TestGroup.MasterId, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(self, cell) },
            new FightFighterShowEvent { Fighter = MonsterActor(monster) },
            new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { monster }, ChallengerCells = { cell, near } } },
            new FightSpellsContextEvent { FighterId = self, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 } } },
            order,
        })
        {
            connection.Server(message);
        }
    }

    // As the server tells the party (D435): the master's fight, on a map, in the millisecond it is shown there.
    private static PartyMemberInFightEvent InFight(long mapId)
    {
        return new PartyMemberInFightEvent { FightId = FightId, MemberId = TestGroup.MasterId, Map = new MapLocation { MapId = mapId }, PartyId = TestGroup.PartyId, Cause = PartyFightCause.Monsters };
    }

    private static ActivityRun Follow(TestPlay play, BotActor follower)
    {
        return follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots));
    }

    // The follower in its master's party, its options turned off: it follows.
    private static async Task<(TestConnection Connection, BotActor Bot, ActivityRun Run)> FollowingAsync(TestPlay play, int cell)
    {
        var (connection, follower) = await TestGroup.FollowerAsync(play, cell);
        var run = Follow(play, follower);

        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));
        Assert.IsType<PartyAutoOptionTwoOffRequest>(await TestGroup.SentAsync(play, connection, 2));

        return (connection, follower, run);
    }

    [Fact]
    public async Task AFollowerJoinsItsMastersFightWhichTheMasterLeavesToItsPartyAndIsReadyOnceTheFollowerIs()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, Far);
        var (followerConnection, follower, run) = await FollowingAsync(play, 301);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = masterConnection.Sent.Count;

        master.FightsAlone = true;
        Placement(masterConnection, TestGroup.MasterId, Far, near, monster);
        followerConnection.Server(InFight(TestPlay.MapId));

        // Measured (D435): the server locks a leader's fight to its party; closing it would keep its follower out.
        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 1)).Option);
        masterConnection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.Equal(new FightJoinRequest { FightId = FightId, FighterId = TestGroup.MasterId }, await TestGroup.SentAsync(play, followerConnection, 3));

        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 2));
        masterConnection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = TestGroup.MasterId, CellId = near } } });
        await TestGroup.UntilAsync(play, () => play.Log.GetSnapshot().Any(record => record.Message.Contains("to be placed before it is ready", StringComparison.Ordinal)), "the master waiting");

        // The follower in the fight, placed by its AI: it sets none of its team's options, which are its master's.
        Placement(followerConnection, TestGroup.FollowerId, 301, Beside(monster, -2, 1), monster);
        var placed = Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, followerConnection, 4));

        followerConnection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = TestGroup.FollowerId, CellId = placed.CellId } } });
        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, followerConnection, 5));
        Assert.Equal(sent + 2, masterConnection.Sent.Count);

        followerConnection.Server(new FightHumanReadyStateEvent { CharacterId = TestGroup.FollowerId, Ready = true });

        Assert.True(Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 3)).Ready);
        Assert.DoesNotContain(followerConnection.Sent, request => request is FightOptionToggleRequest);
        run.Stop();
    }

    [Fact]
    public async Task AMasterIsReadyAtOnceWhenItsFollowerIsOnAnotherMap()
    {
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.NorthMapId);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var sent = connection.Sent.Count;

        master.FightsAlone = true;
        Placement(connection, TestGroup.MasterId, Far, near, monster);

        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 1)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, connection, sent + 2));
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = TestGroup.MasterId, CellId = near } } });

        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, connection, sent + 3));
        Assert.DoesNotContain(play.Log.GetSnapshot(), record => record.Message.Contains("to be placed before it is ready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AMasterWaitsNoLongerForAFollowerThePlayerTookOver()
    {
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var sent = connection.Sent.Count;

        master.FightsAlone = true;
        Placement(connection, TestGroup.MasterId, Far, near, monster);
        Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 1));
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, connection, sent + 2));
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = TestGroup.MasterId, CellId = near } } });
        await TestGroup.UntilAsync(play, () => play.Log.GetSnapshot().Any(record => record.Message.Contains("to be placed before it is ready", StringComparison.Ordinal)), "the master waiting");

        // On its map, out of the fight: the follower would join. The player taking it over, the master waits no longer.
        follower.Pause();

        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, connection, sent + 3));
    }

    [Fact]
    public async Task AFollowerJoinsNoFightOnAnotherMapNorOneItsMasterIsNotIn()
    {
        await using var play = new TestPlay();
        await TestGroup.MasterAsync(play, Far);
        var (connection, follower, run) = await FollowingAsync(play, 301);

        // The master fights nowhere: the party's word alone, which no message ends (D435), joins nothing.
        connection.Server(InFight(TestPlay.MapId));
        await TestGroup.SettleAsync(follower);
        play.Time.Advance(TimeSpan.FromSeconds(10));
        await TestGroup.SettleAsync(follower);

        Assert.Equal(2, connection.Sent.Count);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerAsksToJoinAFightOnceThoughTheServerAnswersNothing()
    {
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, Far);
        var (connection, follower, run) = await FollowingAsync(play, 301);
        var monster = Beside(Far, 8, 0);

        // The master in the placement, not played: the follower's join is all this test looks at.
        Placement(masterConnection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster);
        connection.Server(InFight(TestPlay.NorthMapId));
        await TestGroup.SettleAsync(follower);
        play.Time.Advance(TimeSpan.FromSeconds(10));
        await TestGroup.SettleAsync(follower);

        Assert.Equal(2, connection.Sent.Count);

        connection.Server(InFight(TestPlay.MapId));

        Assert.IsType<FightJoinRequest>(await TestGroup.SentAsync(play, connection, 3));

        for (var wait = 0; wait < 5; wait++)
        {
            play.Time.Advance(TimeSpan.FromSeconds(5));
            await TestGroup.SettleAsync(follower);
        }

        Assert.Equal(3, connection.Sent.Count);
        run.Stop();
    }

    [Fact]
    public async Task AfterALostFightTheFollowerGoesBackToItsMastersMap()
    {
        // D432: brought back elsewhere, the follower goes to its master's map by the world's paths, and the chain gathers again.
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, Far);
        var (connection, follower, run) = await FollowingAsync(play, 301);
        var sent = connection.Sent.Count;

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await TestGroup.UntilAsync(play, () => run.LastInterruption is Interruption.Fight, "the fight cutting the step");

        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        TestGroup.Arrive(connection, TestGroup.FollowerId, TestPlay.MapId, 301);
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.NorthMapId, 500);

        await TestGroup.WalkedAsync(play, connection, TestGroup.FollowerId, sent + 1);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestGroup.SentAsync(play, connection, sent + 2));
        run.Stop();
    }
}
