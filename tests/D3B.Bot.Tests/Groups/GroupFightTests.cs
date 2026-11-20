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

    // The placement as the server shows it to one of the team (D435): its cell and another of its team's, its allies already
    // in, and the party's lock the server sets by itself as a party's leader attacks.
    private static void Placement(TestConnection connection, long self, int cell, int near, int monster, bool partyOnly, params Actor[] allies)
    {
        Placement(connection, self, cell, near, monster, partyOnly, TestPlay.MapId, allies);
    }

    // Measured (D448): the server names the fight's own map, whose id is not the map's the fight began on.
    private static void Placement(TestConnection connection, long self, int cell, int near, int monster, bool partyOnly, long fightMap, params Actor[] allies)
    {
        Placement(connection, self, cell, near, monster, partyOnly ? [FightOption.PartyOnly] : [], fightMap, allies);
    }

    // Measured on the machine's captures (D471, D475): the server lists the team's four options as the placement starts, after
    // its cells, those its leader set in its last fight on among them; null withholds them, as in the instant before.
    private static void Placement(TestConnection connection, long self, int cell, int near, int monster, FightOption[]? on, long fightMap, params Actor[] allies)
    {
        var order = new FightTurnListEvent();

        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = self } });
        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = Monster } });

        var messages = new List<IMessage>
        {
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = fightMap },
            new FightStartingEvent { AttackerId = TestGroup.MasterId, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(self, cell) },
            new FightFighterShowEvent { Fighter = MonsterActor(monster) },
        };

        messages.AddRange(allies.Select(ally => new FightFighterShowEvent { Fighter = ally }));
        messages.Add(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { monster }, ChallengerCells = { cell, near } } });

        if (on is not null)
            messages.AddRange(Options(on));

        messages.Add(new FightSpellsContextEvent { FighterId = self, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 } } });
        messages.Add(order);

        foreach (var message in messages)
            connection.Server(message);
    }

    private static IEnumerable<FightOptionUpdateEvent> Options(FightOption[] on)
    {
        return Enum.GetValues<FightOption>().Select(option => new FightOptionUpdateEvent { Option = option, Enabled = on.Contains(option) });
    }

    // As the server tells the party (D435): the master's fight, on a map, in the millisecond it is shown there.
    private static PartyMemberInFightEvent InFight(long mapId, int fightId = FightId)
    {
        return new PartyMemberInFightEvent { FightId = fightId, MemberId = TestGroup.MasterId, Map = new MapLocation { MapId = mapId }, PartyId = TestGroup.PartyId, Cause = PartyFightCause.Monsters };
    }

    private static ActivityRun Follow(TestPlay play, BotActor follower)
    {
        return follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));
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

    private static bool Logged(TestPlay play, string text)
    {
        return play.Log.GetSnapshot().Any(record => record.Message.Contains(text, StringComparison.Ordinal));
    }

    private static Task WaitingAsync(TestPlay play)
    {
        return TestGroup.UntilAsync(play, () => Logged(play, "to be placed before it is ready"), "the master waiting");
    }

    // The master placed in a fight the server locked to its party: it refused spectators only, and waits or is ready.
    private static async Task<int> PlacedAsync(TestPlay play, TestConnection connection, BotActor master)
    {
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        master.FightsAlone = true;
        Placement(connection, TestGroup.MasterId, Far, near, monster, partyOnly: true);

        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 1)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, connection, sent + 2));
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = TestGroup.MasterId, CellId = near } } });

        return sent;
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
        Placement(masterConnection, TestGroup.MasterId, Far, near, monster, true, TestPlay.EastMapId);
        followerConnection.Server(InFight(TestPlay.MapId));

        // Measured (D435): the server locks a leader's fight to its party; closing it would keep its follower out.
        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 1)).Option);
        masterConnection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.Equal(new FightJoinRequest { FightId = FightId, FighterId = TestGroup.MasterId }, await TestGroup.SentAsync(play, followerConnection, 3));

        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 2));
        masterConnection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = TestGroup.MasterId, CellId = near } } });
        await WaitingAsync(play);

        var waited = play.Time.GetUtcNow();

        // Measured (D445): the follower's client says by itself it is not ready as it joins, which pauses nothing.
        followerConnection.Client(new FightReadyRequest());

        // The follower in its master's fight, placed by its AI: it sets none of its team's options, which are its master's.
        Placement(followerConnection, TestGroup.FollowerId, 301, Beside(monster, -2, 1), monster, true, TestPlay.EastMapId, Character(TestGroup.MasterId, near));
        var placed = Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, followerConnection, 4));

        followerConnection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = TestGroup.FollowerId, CellId = placed.CellId } } });
        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, followerConnection, 5));
        Assert.Equal(sent + 2, masterConnection.Sent.Count);

        followerConnection.Server(new FightHumanReadyStateEvent { CharacterId = TestGroup.FollowerId, Ready = true });

        // Ready as its follower is, not at the end of the AI's 45 s.
        Assert.True(Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 3)).Ready);
        Assert.True(Logged(play, "its followers are placed"));
        Assert.True(play.Time.GetUtcNow() - waited < TimeSpan.FromSeconds(45));
        Assert.DoesNotContain(followerConnection.Sent, request => request is FightOptionToggleRequest);
        Assert.False(follower.IsPaused);
        run.Stop();
    }

    [Fact]
    public async Task APlacementTheStateDoesNotSeeAnsweredDelaysTheReadinessASecondAtMost()
    {
        // Measured (D450): a swap with an ally, which the state does not read, answered the move; the AI waited 3 s for it.
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        master.FightsAlone = true;
        Placement(connection, TestGroup.MasterId, Far, near, monster, partyOnly: false);

        Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 1));
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });
        Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 2));
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, connection, sent + 3));

        var asked = play.Time.GetUtcNow();

        // Nothing the state reads answers it: the readiness comes after a second and the fight's pace, not the answer's 3 s.
        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, connection, sent + 4));
        Assert.True(play.Time.GetUtcNow() - asked < TimeSpan.FromSeconds(2.5));
    }

    [Fact]
    public async Task AMasterIsReadyAtOnceWhenItsFollowerIsOnAnotherMap()
    {
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.NorthMapId);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var sent = await PlacedAsync(play, connection, master);

        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, connection, sent + 3));
        Assert.False(Logged(play, "to be placed before it is ready"));
    }

    [Fact]
    public async Task AMasterWaitsNoLongerForAFollowerThePlayerTookOver()
    {
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 301);

        followerConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var sent = await PlacedAsync(play, connection, master);

        await WaitingAsync(play);

        var waited = play.Time.GetUtcNow();

        // On its map and in the party, out of the fight: the follower would join. The player taking it over, the master waits no longer.
        follower.Pause();

        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, connection, sent + 3));
        Assert.True(Logged(play, "its followers are placed"));
        Assert.True(play.Time.GetUtcNow() - waited < TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task AMasterWaitsForAFollowerOfItsPartyWhoeverLeadsIt()
    {
        // The party formed by hand, its follower leading it (D471): the follower is told of the fight and joins it.
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 301);

        followerConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.FollowerId, TestGroup.FollowerId, TestGroup.MasterId));

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);

        await PlacedAsync(play, connection, master);
        await WaitingAsync(play);
    }

    [Fact]
    public async Task AMasterWaitsNotForAFollowerOutOfItsParty()
    {
        // Out of the party, the follower is told of no fight, and never joins it.
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var sent = await PlacedAsync(play, connection, master);

        Assert.IsType<FightReadyRequest>(await TestGroup.SentAsync(play, connection, sent + 3));
        Assert.False(Logged(play, "to be placed before it is ready"));
    }

    [Fact]
    public async Task AMasterClosesNotItsFightWhileItHasFollowersThoughTheServerDidNotLockItToItsParty()
    {
        // The owner's report of 2026-11-20 (D471): the master's party ended, it fought closed, and the server kept its fight
        // closed into the next, its followers' joins refused. A master with followers closes no fight, so that none is kept
        // closed into the one they join; spectators only are refused.
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var (_, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.NorthMapId);
        var monster = Beside(Far, 8, 0);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var sent = connection.Sent.Count;

        master.FightsAlone = true;
        Placement(connection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, partyOnly: false);

        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 1)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, connection, sent + 2));
    }

    [Fact]
    public async Task ACharacterClosesNotAFightTheServerLockedToItsPartyThoughNobodyFollowsIt()
    {
        // Measured (D471): the server locks to its party the fight of any member that attacks, its leader or not.
        await using var play = new TestPlay();
        var (connection, master) = await TestGroup.MasterAsync(play, Far);
        var monster = Beside(Far, 8, 0);
        var sent = connection.Sent.Count;

        master.FightsAlone = true;
        Placement(connection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, partyOnly: true);

        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 1)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, connection, sent + 2));
    }

    [Fact]
    public async Task AMasterOpensTheFightTheServerKeptClosedAndItsFollowerJoinsItOnceOpen()
    {
        // Measured on the owner's game of 2026-11-20 (D471): the server keeps a team's options from one fight of its leader
        // to the next; the fight after one D3B closed began closed, and the followers' joins sent then were refused.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, Far);
        var (followerConnection, follower, run) = await FollowingAsync(play, 301);
        var monster = Beside(Far, 8, 0);
        var sent = masterConnection.Sent.Count;

        master.FightsAlone = true;
        Placement(masterConnection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, [FightOption.PartyOnly, FightOption.Closed, FightOption.NoSpectators], TestPlay.EastMapId);
        followerConnection.Server(InFight(TestPlay.MapId));

        Assert.Equal(FightOption.Closed, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 1)).Option);
        await TestGroup.UntilAsync(play, () => follower.State.PartyFight is not null, "the follower told of the fight");
        await TestGroup.SettleAsync(master, follower);

        // Closed, the fight would refuse the follower: it waits for it to open.
        Assert.Equal(2, followerConnection.Sent.Count);

        // Opened, the follower joins at once, the clock still: the master's fight opening wakes it.
        masterConnection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = false });
        await TestGroup.SettleAsync(master, follower);

        Assert.Equal(new FightJoinRequest { FightId = FightId, FighterId = TestGroup.MasterId }, Assert.Single(followerConnection.Sent.Skip(2)));

        // Spectators already refused, as the server kept them: the master toggles nothing else, and places itself.
        Assert.IsType<FightPlacementPositionRequest>(await TestGroup.SentAsync(play, masterConnection, sent + 2));
        run.Stop();
    }

    // A group the game follows (D476), its follower following, its options on.
    private static async Task<(TestConnection Master, BotActor MasterBot, TestConnection Follower, BotActor FollowerBot, ActivityRun Run)> FollowedByTheGameAsync(TestPlay play)
    {
        var (masterConnection, master, connection, follower) = await TestGroup.FollowedByTheGameAsync(play, Far, 301);
        var run = Follow(play, follower);

        connection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));
        Assert.IsType<PartyFollowLeaderRequest>(await TestGroup.SentAsync(play, connection, 3));
        connection.Response(new PartyFollowLeaderResponse());
        await TestGroup.SettleAsync(follower);

        return (masterConnection, master, connection, follower, run);
    }

    [Fact]
    public async Task AFollowerTheGameFollowsLeavesItsJoinToTheServer()
    {
        // Measured on the owner's game of 2026-11-20 (D476): the server joined the followers in the millisecond of the attack.
        await using var play = new TestPlay();
        var (masterConnection, master, followerConnection, follower, run) = await FollowedByTheGameAsync(play);
        var monster = Beside(Far, 8, 0);

        master.FightsAlone = true;
        Placement(masterConnection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, true, TestPlay.EastMapId);
        followerConnection.Server(InFight(TestPlay.MapId));
        await TestGroup.SettleAsync(master, follower);
        play.Time.Advance(TimeSpan.FromMilliseconds(500));
        await TestGroup.SettleAsync(master, follower);
        Placement(followerConnection, TestGroup.FollowerId, 301, Beside(monster, -3, 0), monster, true, TestPlay.EastMapId);
        await TestGroup.SettleAsync(master, follower);
        play.Time.Advance(TimeSpan.FromSeconds(2));
        await TestGroup.SettleAsync(master, follower);

        Assert.True(follower.State.IsFighting);
        Assert.DoesNotContain(followerConnection.Sent, request => request is FightJoinRequest);
        run.Stop();
    }

    [Fact]
    public async Task AFollowerTheServerDidNotJoinJoinsByRequestAndTurnsItsOptionsOnAgain()
    {
        await using var play = new TestPlay();
        var (masterConnection, master, followerConnection, follower, run) = await FollowedByTheGameAsync(play);
        var monster = Beside(Far, 8, 0);

        master.FightsAlone = true;
        Placement(masterConnection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, true, TestPlay.EastMapId);
        followerConnection.Server(InFight(TestPlay.MapId));

        Assert.Equal(new FightJoinRequest { FightId = FightId, FighterId = TestGroup.MasterId }, await TestGroup.SentAsync(play, followerConnection, 4));

        // The server answers nothing: once its fight is over, the follower's options go on again.
        masterConnection.Server(new ContextDestroyEvent());
        masterConnection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.MapId, Far);
        followerConnection.Server(new ContextDestroyEvent());
        followerConnection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        TestGroup.Arrive(followerConnection, TestGroup.FollowerId, TestPlay.MapId, 301);

        Assert.IsType<PartyAutoOptionOneOnRequest>(await TestGroup.SentAsync(play, followerConnection, 5));
        Assert.IsType<PartyAutoOptionTwoOnRequest>(await TestGroup.SentAsync(play, followerConnection, 6));
        run.Stop();
    }

    [Fact]
    public async Task AFollowerWaitsForItsMastersFightToListItsOptionsBeforeJoiningIt()
    {
        // Measured (D475): the placement shows its cells before its options, so that a fight kept closed looks open in between;
        // a join sent then was refused, and the follower never tried again.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, Far);
        var (followerConnection, follower, run) = await FollowingAsync(play, 301);
        var monster = Beside(Far, 8, 0);

        master.FightsAlone = true;
        Placement(masterConnection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, null, TestPlay.EastMapId);
        followerConnection.Server(InFight(TestPlay.MapId));

        await TestGroup.UntilAsync(play, () => follower.State.PartyFight is not null && master.State.Fight is not null, "the follower told of the fight");
        await TestGroup.SettleAsync(master, follower);

        Assert.Equal(2, followerConnection.Sent.Count);

        foreach (var listed in Options([FightOption.PartyOnly]))
            masterConnection.Server(listed);

        await TestGroup.SettleAsync(master, follower);

        Assert.Equal(new FightJoinRequest { FightId = FightId, FighterId = TestGroup.MasterId }, Assert.Single(followerConnection.Sent.Skip(2)));
        run.Stop();
    }

    [Fact]
    public async Task AFollowersOwnFightWithoutItsMasterIsClosedAsOneAlone()
    {
        // An aggression on its way: its master is not in the fight, whose options are the follower's own.
        await using var play = new TestPlay();
        await TestGroup.MasterAsync(play, Far, TestPlay.NorthMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, Far);
        var monster = Beside(Far, 8, 0);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var sent = connection.Sent.Count;

        follower.FightsAlone = true;
        Placement(connection, TestGroup.FollowerId, Far, Beside(monster, -3, 0), monster, partyOnly: false);

        Assert.Equal(FightOption.Closed, Assert.IsType<FightOptionToggleRequest>(await TestGroup.SentAsync(play, connection, sent + 1)).Option);
    }

    [Fact]
    public async Task AFollowerJoinsNoFightItsMasterIsNotInNorOneThePartyToldOfBefore()
    {
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, Far);
        var (connection, follower, run) = await FollowingAsync(play, 301);
        var monster = Beside(Far, 8, 0);

        // The master fights nowhere: the party's word alone, which no message ends (D435), joins nothing.
        connection.Server(InFight(TestPlay.MapId));
        await TestGroup.SettleAsync(follower);
        play.Time.Advance(TimeSpan.FromSeconds(10));
        await TestGroup.SettleAsync(follower);

        Assert.Equal(2, connection.Sent.Count);

        // The master's next fight on the map: that word is over, and the follower waits for the party's next.
        Placement(masterConnection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, partyOnly: true);
        await TestGroup.SettleAsync(follower);
        play.Time.Advance(TimeSpan.FromSeconds(10));
        await TestGroup.SettleAsync(follower);

        Assert.Equal(2, connection.Sent.Count);

        connection.Server(InFight(TestPlay.MapId, FightId + 1));

        Assert.Equal(new FightJoinRequest { FightId = FightId + 1, FighterId = TestGroup.MasterId }, await TestGroup.SentAsync(play, connection, 3));
        run.Stop();
    }

    [Fact]
    public async Task AFollowerAsksToJoinAFightOnceThoughTheServerAnswersNothing()
    {
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, Far);
        var (connection, follower, run) = await FollowingAsync(play, 301);
        var monster = Beside(Far, 8, 0);

        // The master in the placement, not played: the follower's join is all this test looks at. Another map's fight first.
        Placement(masterConnection, TestGroup.MasterId, Far, Beside(monster, -3, 0), monster, partyOnly: true);
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
    public async Task AFollowerBackFromAFightGoesToTheMapItsMasterIsOn()
    {
        // D432: out of a fight away from its master, as after a lost one, the follower goes to its master's map by the world's
        // paths; the master's gate waits for it after its next map change, as GroupGateTests show.
        await using var play = new TestPlay();
        var (masterConnection, _) = await TestGroup.MasterAsync(play, Far);
        var (connection, _, run) = await FollowingAsync(play, 301);
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
