using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Fights;

/// <summary>A fight played as the captures measured it: the player attacking monsters (client.md, D178).</summary>
public sealed class FightEventsTests
{
    private const long Self = TestPlay.CharacterId;
    private const long Monster = -1;
    private const long Turret = -2;
    private const long FightMapId = 191_104_008;

    private static readonly CharacterState _chosen = CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character);

    private static CharacterState Apply(CharacterState state, params IMessage[] messages)
    {
        return messages.Aggregate(state, (current, message) => CharacterEvents.Apply(current, RelayDirection.FromServer, message));
    }

    private static Characteristic Usable(int id, int left, int spent = 0)
    {
        return new Characteristic { Id = id, Usable = new UsableCharacteristic { Number4 = left + spent, Number6 = -spent, Spent = spent } };
    }

    private static Characteristic Detailed(int id, int value)
    {
        return new Characteristic { Id = id, Detailed = new DetailedCharacteristic { Number7 = value } };
    }

    private static Characteristic Value(int id, long total)
    {
        return new Characteristic { Id = id, Value = new CharacteristicValue { Total = total } };
    }

    private static Actor Character(int cell, FightTeam team = FightTeam.Challenger)
    {
        return new Actor
        {
            Id = Self,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Named = new NamedFighter { Character = new FighterCharacter { BreedId = 15, Level = 18 } },
                    Stats = new FightCharacteristics { Characteristics = { Usable(1, 6), Usable(23, 3), Detailed(0, 140), Detailed(11, 58) } },
                    Spawn = new FighterSpawn { Alive = true, Team = team },
                },
            },
        };
    }

    private static SummonedFighter Summon(long id, int monsterId, int cell)
    {
        return new SummonedFighter
        {
            Entity = new SummonedEntity { Monster = new SummonedMonster { MonsterId = monsterId, Grade = 1 } },
            Stats = new FightCharacteristics { Summoned = true, SummonerId = Self, Characteristics = { Value(1, 6), Value(0, 15) } },
            Positions = { new FighterSpawn { Alive = true, Team = FightTeam.Challenger, Position = new Actor { Id = id, Disposition = new ActorDisposition { CellId = cell } } } },
        };
    }

    private static GameActionFightEvent Summoned(SummonedFighter summon)
    {
        return new GameActionFightEvent { ActionId = 181, SourceId = Self, Summons = new FightSummons { Fighters = new SummonedFighters { Summons = { summon } } } };
    }

    private static Actor MonsterActor(long id, int cell, int life)
    {
        return new Actor
        {
            Id = id,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Ai = new AiFighter { Monster = new MonsterFighter { MonsterId = 3_271, Grade = 3, Level = 3 } },
                    Stats = new FightCharacteristics { Characteristics = { Value(1, 2), Value(23, 3), Value(0, life) } },
                    Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Defender },
                },
            },
        };
    }

    private static FightTurnListEvent Order(params (long Id, bool Dead)[] fighters)
    {
        var order = new FightTurnListEvent();

        order.Entries.AddRange(fighters.Select(fighter => new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = fighter.Id, Dead = fighter.Dead } }));

        return order;
    }

    /// <summary>The fight's context, its map and its placement, as the server sends them once the player attacked.</summary>
    private static CharacterState Placed()
    {
        return Apply(
            _chosen,
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = FightMapId },
            new FightStartingEvent { AttackerId = Self, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(314) },
            new FightFighterShowEvent { Fighter = MonsterActor(Monster, 385, 21) },
            new FightPlacementPossiblePositionsEvent
            {
                Cells = new PlacementCells { DefenderCells = { 385, 345 }, ChallengerCells = { 314, 300 } },
            },
            Order((Self, false), (Monster, false)));
    }

    /// <summary>The placement over, the fight started and the character's first turn begun.</summary>
    private static CharacterState Playing()
    {
        return Apply(
            Placed(),
            new FightHumanReadyStateEvent { CharacterId = Self, Ready = true },
            new FightStartEvent(),
            new FightNewRoundEvent { Round = 1 },
            new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 },
            new FightTurnStartPlayingEvent());
    }

    [Fact]
    public void AFightsContextGivesTheCharacterAFightInPlacement()
    {
        var state = Apply(_chosen, new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });

        Assert.True(state.IsFighting);
        Assert.Equal(FightPhase.Placement, state.Fight?.Phase);
        Assert.Empty(state.Fight!.Fighters);
    }

    [Fact]
    public void ThePlacementGivesEachTeamsCellsAndWhereEveryFighterStands()
    {
        var fight = Placed().Fight!;

        Assert.Equal(FightTeam.Challenger, fight.Team);
        Assert.Equal([314, 300], fight.OwnCells);
        Assert.Equal([385, 345], fight.DefenderCells);
        Assert.Equal([Self, Monster], fight.TurnOrder);

        var self = fight.Fighters[Self];

        Assert.Equal((314, 198, 198, 6, 3, 15, 18), (self.Cell, self.Life, self.MaxLife, self.ActionPoints, self.MovementPoints, self.BreedId, self.Level));

        var monster = fight.Fighters[Monster];

        Assert.Equal((FightTeam.Defender, 385, 21, 2, 3, 3_271, 3), (monster.Team, monster.Cell, monster.Life, monster.ActionPoints, monster.MovementPoints, monster.MonsterId, monster.Grade));
    }

    [Fact]
    public void AMoveDuringThePlacementPlacesTheFighterWhereTheServerSays()
    {
        var moved = Apply(
            Placed(),
            new EntitiesDispositionEvent
            {
                Dispositions =
                {
                    new ActorDisposition { EntityId = Monster, CellId = 385 },
                    new ActorDisposition { EntityId = Self, CellId = 300 },
                    new ActorDisposition { EntityId = -9, CellId = 345 },
                },
            });

        Assert.Equal(300, moved.Fight!.Fighters[Self].Cell);
        Assert.Equal(385, moved.Fight.Fighters[Monster].Cell);
        Assert.False(moved.Fight.Fighters.ContainsKey(-9));
    }

    [Fact]
    public void TheTurnsSayWhoPlaysAndWhenTheCharacterMayAct()
    {
        var playing = Playing();

        Assert.Equal(FightPhase.Fighting, playing.Fight!.Phase);
        Assert.False(playing.Fight.IsReady);
        Assert.Equal((Self, 1, true), (playing.Fight.PlayingId, playing.Fight.Round, playing.Fight.IsOwnTurn));

        var ended = Apply(playing, new FightTurnEndEvent { FighterId = Self });

        Assert.Equal((null, false), (ended.Fight!.PlayingId, ended.Fight.IsOwnTurn));

        var theirs = Apply(ended, new FightTurnEvent { FighterId = Monster, Duration = 290, Round = 1 }, new FightTurnStartPlayingEvent());

        Assert.Equal((Monster, false), (theirs.Fight!.PlayingId, theirs.Fight.IsOwnTurn));
    }

    [Fact]
    public void TheCharactersWalkInAFightMovesItsFighterWithNoPathToConfirm()
    {
        var walked = Apply(Playing(), new MapMovementEvent { ActorId = Self, Cells = { 314, 329 } });

        Assert.Equal(329, walked.Fight!.Fighters[Self].Cell);
        Assert.False(walked.IsWalking);
        Assert.Null(walked.Cell);
    }

    [Fact]
    public void TheCharacteristicsGiveTheApAndMpLeftAndTheAnnouncedSpendingIsNotCountedAgain()
    {
        var spent = Apply(
            Playing(),
            new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, 3, spent: 3) } } },
            new GameActionFightEvent { ActionId = 102, SourceId = Self, Points = new FightPointsVariation { TargetId = Self, Delta = -3 } });

        Assert.Equal((3, 3), (spent.Fight!.Fighters[Self].ActionPoints, spent.Fight.Fighters[Self].MovementPoints));
    }

    [Fact]
    public void ABlowTakesLifeAndADeathLeavesTheFighterOnNoCell()
    {
        var hit = Apply(
            Playing(),
            new GameActionFightEvent { ActionId = 300, SourceId = Self, Cast = new FightCast { Spell = new FightCastSpell { SpellId = 13_858 }, CellId = 385 } },
            new GameActionFightEvent { ActionId = 99, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 15, PermanentLoss = 3, Element = 2 } });

        Assert.Equal((6, 18), (hit.Fight!.Fighters[Monster].Life, hit.Fight.Fighters[Monster].MaxLife));

        var dead = Apply(
            hit,
            new GameActionFightEvent { ActionId = 103, SourceId = Self, Death = new FightDeath { TargetId = Monster } },
            Order((Self, false), (Monster, true)));

        Assert.False(dead.Fight!.Fighters[Monster].IsAlive);
        Assert.Null(dead.Fight.Fighters[Monster].Cell);
        Assert.Equal([Self, Monster], dead.Fight.TurnOrder);
    }

    [Fact]
    public void AHealNeverGivesMoreThanTheMostLife()
    {
        var healed = Apply(
            Playing(),
            new GameActionFightEvent { ActionId = 99, SourceId = Monster, LifeLost = new FightLifeLost { TargetId = Self, Loss = 50 } },
            new GameActionFightEvent { ActionId = 108, SourceId = Self, LifeGained = new FightLifeGained { TargetId = Self, Amount = 36 } },
            new GameActionFightEvent { ActionId = 108, SourceId = Self, LifeGained = new FightLifeGained { TargetId = Self, Amount = 36 } });

        Assert.Equal(198, healed.Fight!.Fighters[Self].Life);
    }

    [Fact]
    public void ATeleportAndASwapMoveTheFighters()
    {
        var teleported = Apply(Playing(), new GameActionFightEvent { ActionId = 4, SourceId = Self, Teleport = new FightTeleport { TargetId = Self, CellId = 384 } });

        Assert.Equal(384, teleported.Fight!.Fighters[Self].Cell);

        var swapped = Apply(teleported, new GameActionFightEvent { ActionId = 8, SourceId = Self, Swap = new FightSwap { TargetId = Monster } });

        Assert.Equal((385, 384), (swapped.Fight!.Fighters[Self].Cell, swapped.Fight.Fighters[Monster].Cell));
    }

    [Fact]
    public void ASummonComesIntoTheFightWithItsSummonerAndCell()
    {
        var summoned = Apply(Playing(), Summoned(Summon(Turret, 5_836, 286)));

        var turret = summoned.Fight!.Fighters[Turret];

        Assert.Equal((FightTeam.Challenger, 286, 15, 6, 5_836, 1, Self), (turret.Team, turret.Cell, turret.Life, turret.ActionPoints, turret.MonsterId, turret.Grade, turret.SummonerId));
    }

    [Fact]
    public void ASummonSummonedAgainUnderAnotherMonsterKeepsItsIdAndTakesTheNewOne()
    {
        // Measured: a turret summoned again under another monster's id, its own id the same, on another cell.
        var again = Apply(Playing(), Summoned(Summon(Turret, 5_836, 286)), Summoned(Summon(Turret, 5_835, 328)));

        Assert.Equal((5_835, 328), (again.Fight!.Fighters[Turret].MonsterId, again.Fight.Fighters[Turret].Cell));
        Assert.Equal(3, again.Fight.Fighters.Count);
    }

    [Fact]
    public void TheCharactersOwnFighterSaysItsTeamAndPlacementCells()
    {
        var defending = Apply(
            _chosen,
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new FightFighterShowEvent { Fighter = Character(385, FightTeam.Defender) },
            new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { 385, 345 }, ChallengerCells = { 314, 300 } } });

        Assert.Equal(FightTeam.Defender, defending.Fight!.Team);
        Assert.Equal([385, 345], defending.Fight.OwnCells);
    }

    [Fact]
    public void TheFightersSynchronizedKeepWhatTheActionsToldAndTheOthersGo()
    {
        var hit = Apply(
            Playing(),
            new FightFighterShowEvent { Fighter = MonsterActor(-3, 345, 18) },
            new GameActionFightEvent { ActionId = 99, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 15 } });
        var synchronized = Apply(
            hit,
            new FightSynchronizeEvent { Fighters = { Character(300), new Actor { Id = Monster, Disposition = new ActorDisposition { CellId = 371 } } } });

        var monster = synchronized.Fight!.Fighters[Monster];

        Assert.Equal([Monster, Self], synchronized.Fight.Fighters.Keys);
        Assert.Equal((371, 6, 21, FightTeam.Defender, 3_271), (monster.Cell, monster.Life, monster.MaxLife, monster.Team, monster.MonsterId));
        Assert.Equal(300, synchronized.Fight.Fighters[Self].Cell);
    }

    [Fact]
    public void AnEndWithoutTheCharactersResultEndsTheFightWithNoGains()
    {
        var ended = Apply(Playing(), new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });

        Assert.Equal(FightPhase.Ended, ended.Fight!.Phase);
        Assert.Null(ended.Fight.End);
    }

    [Fact]
    public void TheEndGivesTheCharactersOutcomeAndGainsUntilTheRoleplay()
    {
        var result = new FightResult
        {
            Outcome = FightOutcome.Won,
            Fighter = new FightResultFighter
            {
                FighterId = Self,
                Alive = true,
                Character = new FightResultCharacter { Level = 18, Data = { new FightResultData { Experience = new FightExperience { Experience = 124_909, Gained = 77 } } } },
            },
            Loot = new FightLoot
            {
                Kamas = 7,
                Objects =
                {
                    new FightLootObjects { MonsterId = 3_271, Objects = { new FightLootObject { ItemId = 16_518, Quantity = 1 } } },
                    new FightLootObjects { MonsterId = 3_272, Objects = { new FightLootObject { ItemId = 16_518, Quantity = 1 }, new FightLootObject { ItemId = 1_984, Quantity = 2 } } },
                },
            },
        };
        var ended = Apply(Playing(), new FightEndEvent { Results = { result, new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });

        Assert.Equal(FightPhase.Ended, ended.Fight!.Phase);
        Assert.Equal((FightOutcome.Won, 77, 7), (ended.Fight.End!.Outcome, ended.Fight.End.Experience, ended.Fight.End.Kamas));
        Assert.Equal([KeyValuePair.Create(1_984, 2), KeyValuePair.Create(16_518, 2)], ended.Fight.End.Items.ToArray());

        var over = Apply(ended, new ContextDestroyEvent(), new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });

        Assert.Null(over.Fight);
        Assert.False(over.IsFighting);
    }

    [Fact]
    public void WhatTheServerSaysOfAFighterItNeverShowedChangesNothing()
    {
        var playing = Playing();
        var unknown = Apply(
            playing,
            new MapMovementEvent { ActorId = -7, Cells = { 100, 114 } },
            new GameActionFightEvent { ActionId = 103, SourceId = Self, Death = new FightDeath { TargetId = -7 } },
            new FightRefreshCharacterStatsEvent { FighterId = -7, Stats = new FightCharacteristics() });

        Assert.Equal(playing.Fight!.Fighters, unknown.Fight!.Fighters);
        Assert.Same(playing, Apply(playing, new SequenceStartEvent { AuthorId = Monster }, new FightHumanReadyStateEvent { CharacterId = -9, Ready = true }));
    }
}
