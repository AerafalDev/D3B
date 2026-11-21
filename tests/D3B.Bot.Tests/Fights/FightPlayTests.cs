using D3B.Bot.Fights;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Fights;

/// <summary>The AI playing a fight through the bot, from the placement to the end, the server and the client played as the captures measured them (D202).</summary>
public sealed class FightPlayTests : IAsyncDisposable
{
    private const long Self = TestPlay.CharacterId;
    private const long Monster = -1;
    private const long Knight = -2;
    private const int Far = 300;

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static int Beside(int cell, int dx, int dy)
    {
        var point = MapGeometry.PointOf(cell);

        Assert.True(MapGeometry.TryGetCell(new MapPoint(point.X + dx, point.Y + dy), out var found));

        return found;
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

    private static Actor Character(int cell)
    {
        // No MP, so that only the placement brings the character within its blow's range.
        return new Actor
        {
            Id = Self,
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
        // A monster the pack lacks: the AI knows none of its spells.
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

    private static Actor CompanionActor(int cell)
    {
        // The character's companion, such as the Astrub Knight, with no MP: it strikes from where it stands, or nothing.
        return new Actor
        {
            Id = Knight,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Companion = new CompanionFighter { MasterId = Self },
                    Stats = new FightCharacteristics { Characteristics = { Value(1, 6), Value(23, 0), Value(0, 90) } },
                    Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Challenger },
                },
            },
        };
    }

    private static FightTurnListEvent Order()
    {
        var order = new FightTurnListEvent();

        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = Self } });
        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = Monster } });

        return order;
    }

    private static void Placement(TestConnection connection, int near, int monster)
    {
        foreach (var message in new IMessage[]
        {
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = TestPlay.MapId },
            new FightStartingEvent { AttackerId = Self, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(Far) },
            new FightFighterShowEvent { Fighter = MonsterActor(monster) },
            new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { monster }, ChallengerCells = { Far, near } } },
            new FightSpellsContextEvent { FighterId = Self, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 } } },
            Order(),
        })
        {
            connection.Server(message);
        }
    }

    private static void Cast(TestConnection connection, int cell, int apLeft, int actionId)
    {
        // As the server answers a cast, then the client once it showed it (client.md).
        connection.Server(new SequenceStartEvent { Type = SequenceType.Spell, AuthorId = Self });
        connection.Server(new GameActionFightEvent { ActionId = 300, SourceId = Self, Cast = new FightCast { Spell = new FightCastSpell { SpellId = TestPlay.BlowSpellId, LevelId = TestPlay.BlowLevelId }, CellId = cell, TargetId = Monster } });
        connection.Server(new GameActionFightEvent { ActionId = 97, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 11, Element = 1 } });
        connection.Server(new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, apLeft, spent: 6 - apLeft), Usable(23, 0) } } });
        connection.Server(new SequenceEndEvent { ActionId = actionId, Type = SequenceType.Spell, AuthorId = Self });
        connection.Client(new GameActionAcknowledgementRequest { ActionId = actionId, Valid = true });
    }

    private static void Struck(TestConnection connection, int cell, int apLeft, int actionId)
    {
        // As the server answered the blows captured (D218): a sequence of its own, the blow naming no spell.
        connection.Server(new SequenceStartEvent { Type = SequenceType.Weapon, AuthorId = Self });
        connection.Server(new GameActionFightEvent { ActionId = 303, SourceId = Self, Cast = new FightCast { CellId = cell, TargetId = Monster } });
        connection.Server(new GameActionFightEvent { ActionId = 97, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 22, Element = 1 } });
        connection.Server(new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, apLeft, spent: 6 - apLeft), Usable(23, 0) } } });
        connection.Server(new SequenceEndEvent { ActionId = actionId, Type = SequenceType.Weapon, AuthorId = Self });
        connection.Client(new GameActionAcknowledgementRequest { ActionId = actionId, Valid = true });
    }

    private async Task<IMessage> SentAsync(TestConnection connection, int count)
    {
        // The AI waits the client's display and the fight's pace before each request: the clock moves until it sent.
        var deadline = DateTime.UtcNow + Polling.Deadline;

        while (connection.Sent.Count < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"D3B sent {connection.Sent.Count} requests, not {count}.");
            _play.Time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return connection.Sent[count - 1];
    }

    private async Task<int> StartTurnAsync(TestConnection connection, BotActor bot, int monster)
    {
        // The fight closed, the character placed and ready, then its first turn begins.
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        bot.FightsAlone = true;
        Placement(connection, near, monster);
        await SentAsync(connection, sent + 1);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });
        await SentAsync(connection, sent + 2);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        await SentAsync(connection, sent + 3);
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = Self, CellId = near } } });
        await SentAsync(connection, sent + 4);
        connection.Server(new FightHumanReadyStateEvent { CharacterId = Self, Ready = true });
        connection.Server(new FightStartEvent());
        connection.Server(new FightNewRoundEvent { Round = 1 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());

        return sent;
    }

    private async Task<int> CompanionsTurnAsync(TestConnection connection, BotActor bot, int monster, bool listed)
    {
        // The character's turn played, its two blows, then its companion's turn begins, the companion beside the monster.
        var sent = await StartTurnAsync(connection, bot, monster);

        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Cast(connection, monster, apLeft: 3, actionId: 1);
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));
        Cast(connection, monster, apLeft: 0, actionId: 2);
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightFighterShowEvent { Fighter = CompanionActor(Beside(monster, 0, -2)) });

        if (listed)
            connection.Server(new FightSpellsContextEvent { CharacterId = Self, FighterId = Knight, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 } } });

        connection.Server(new FightTurnEvent { FighterId = Knight, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());

        return sent;
    }

    private async Task LoggedAsync(string message)
    {
        var deadline = DateTime.UtcNow + Polling.Deadline;

        while (!_play.Log.GetSnapshot().Any(record => record.Message == message))
        {
            Assert.True(DateTime.UtcNow < deadline, $"The log never said: {message}");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TheAIClosesTheFightPlacesTheCharacterReadiesItCastsAndEndsItsTurn()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        bot.FightsAlone = true;
        Placement(connection, near, monster);

        Assert.Equal(FightOption.Closed, Assert.IsType<FightOptionToggleRequest>(await SentAsync(connection, sent + 1)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });
        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await SentAsync(connection, sent + 2)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });

        var placed = Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 3));

        Assert.Equal((near, Self), (placed.CellId, placed.FighterId));
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = Self, CellId = near } } });
        Assert.True(Assert.IsType<FightReadyRequest>(await SentAsync(connection, sent + 4)).Ready);
        connection.Server(new FightHumanReadyStateEvent { CharacterId = Self, Ready = true });

        connection.Server(new FightStartEvent());
        connection.Server(new FightNewRoundEvent { Round = 1 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());

        // Two blows of 3 AP on the monster, from the cell the placement chose, then the turn ends: nothing is left to do.
        var first = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));

        Assert.Equal((TestPlay.BlowSpellId, monster), (first.SpellId, first.CellId));
        Cast(connection, monster, apLeft: 3, actionId: 1);

        var second = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));

        Assert.Equal((TestPlay.BlowSpellId, monster), (second.SpellId, second.CellId));
        Cast(connection, monster, apLeft: 0, actionId: 2);

        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightTurnEvent { FighterId = Monster, Duration = 300, Round = 1 });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();

        // The behavior writes the turn's end once the server's answer reached it, on its own task, after the bot's loop handled it.
        await LoggedAsync($"Bot {Self}: ends its turn after 2 actions");

        Assert.Equal(sent + 7, connection.Sent.Count);
        Assert.False(bot.IsPaused);
        Assert.DoesNotContain(_play.Log.GetSnapshot(), record => record.Message.Contains("did not show", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheAIEndsItsTurnWithoutTheFightsPaceOnceItsLastActionIsShown()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Cast(connection, monster, apLeft: 3, actionId: 1);
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));
        Cast(connection, monster, apLeft: 0, actionId: 2);

        // The clock stands still: the turn's end waits for nothing once the last blow was shown (D479).
        var deadline = DateTime.UtcNow + Polling.Deadline;

        while (connection.Sent.Count < sent + 7)
        {
            Assert.True(DateTime.UtcNow < deadline, "The turn's end waited for the clock.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.IsType<FightTurnFinishRequest>(connection.Sent[sent + 6]);
    }

    [Fact]
    public async Task ATurnThatKilledTheLastEnemyLeavesTheFightsEndToTheServer()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Cast(connection, monster, apLeft: 3, actionId: 1);
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));
        connection.Server(new SequenceStartEvent { Type = SequenceType.Spell, AuthorId = Self });
        connection.Server(new GameActionFightEvent { ActionId = 300, SourceId = Self, Cast = new FightCast { Spell = new FightCastSpell { SpellId = TestPlay.BlowSpellId, LevelId = TestPlay.BlowLevelId }, CellId = monster, TargetId = Monster } });
        connection.Server(new GameActionFightEvent { ActionId = 103, SourceId = Self, Death = new FightDeath { TargetId = Monster } });
        connection.Server(new SequenceEndEvent { ActionId = 2, Type = SequenceType.Spell, AuthorId = Self });
        connection.Client(new GameActionAcknowledgementRequest { ActionId = 2, Valid = true });
        await bot.ProcessedAsync();

        // The server ends the fight within the pace: no turn's end follows it.
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();
        _play.Time.Advance(BotDelays.Measured.FightAction + BotDelays.Measured.Answer);
        await bot.ProcessedAsync();

        Assert.DoesNotContain(connection.Sent.Skip(sent + 6), message => message is FightTurnFinishRequest);
    }

    [Fact]
    public async Task TheTurnsEndTheClientSendsByItselfIsWithheldAndTheAIPlaysOn()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Cast(connection, monster, apLeft: 3, actionId: 1);

        // As the owner's captures measured (D366): the client, inactive, ends the AI's turn by itself, though no one touched it.
        var automatic = new FightTurnFinishRequest { Automatic = true };

        connection.Client(automatic);

        Assert.Equal(automatic, Assert.Single(connection.Withheld));
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));
        Cast(connection, monster, apLeft: 0, actionId: 2);
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
        Assert.False(bot.IsPaused);
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();
    }

    [Fact]
    public async Task TheAILogsTheFightsChallengeOnceThenItsResult()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        // Measured (D461): the list as the fight starts, its targets changed as a turn begins, its result before the end.
        var challenge = new ChallengeInformation { State = ChallengeState.Running, ChallengeId = 3, Bonus = 85, Targets = { new ChallengeTarget { FighterId = Monster, CellId = monster } } };

        connection.Server(new ChallengeListEvent { Challenges = { challenge } });
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        connection.Server(new ChallengeTargetsEvent { Challenge = challenge });
        Cast(connection, monster, apLeft: 3, actionId: 1);
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));
        Cast(connection, monster, apLeft: 0, actionId: 2);
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new ChallengeResultEvent { ChallengeId = 3, Success = true });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();

        await LoggedAsync($"Bot {Self}: the fight's challenge 3, bonus 85 %, 1 targets");
        await LoggedAsync($"Bot {Self}: challenge 3 won");

        Assert.Single(_play.Log.GetSnapshot(), record => record.Message.Contains("the fight's challenge", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AChallengeListedDuringATurnTheFightEndsInIsLoggedBeforeItsResult()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        // The list comes once the AI is in its turn, and the fight ends in that turn, the monster killed by the cast.
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        connection.Server(new ChallengeListEvent { Challenges = { new ChallengeInformation { State = ChallengeState.Running, ChallengeId = 3, Bonus = 85 } } });
        Cast(connection, monster, apLeft: 3, actionId: 1);
        connection.Server(new ChallengeResultEvent { ChallengeId = 3, Success = true });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();

        await LoggedAsync($"Bot {Self}: challenge 3 won");

        var messages = _play.Log.GetSnapshot().Select(record => record.Message).ToList();
        var listed = messages.IndexOf($"Bot {Self}: the fight's challenge 3, bonus 85 %, 0 targets");

        Assert.InRange(listed, 0, messages.IndexOf($"Bot {Self}: challenge 3 won") - 1);
    }

    [Fact]
    public async Task AChallengeTheFightEndsWithNoResultForIsLoggedSo()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        connection.Server(new ChallengeListEvent { Challenges = { new ChallengeInformation { State = ChallengeState.Running, ChallengeId = 41, Bonus = 60 } } });
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Cast(connection, monster, apLeft: 3, actionId: 1);
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));
        Cast(connection, monster, apLeft: 0, actionId: 2);
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();

        await LoggedAsync($"Bot {Self}: challenge 41 with no result");
    }

    private async Task<(TestConnection Connection, int Sent)> ChallengedTurnAsync(int challenge, ChallengePlay play, bool secondTurn = false)
    {
        // An activity playing for the profile's challenges, the fight's challenge listed before the character's first turn.
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);

        bot.StartActivity(new RewardActivity { Reward = ChallengeReward.Client, Play = play });
        await bot.ProcessedAsync();

        var sent = connection.Sent.Count;

        Placement(connection, near, monster);
        await SentAsync(connection, sent + 1);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });
        await SentAsync(connection, sent + 2);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        await SentAsync(connection, sent + 3);
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = Self, CellId = near } } });
        await SentAsync(connection, sent + 4);
        connection.Server(new FightHumanReadyStateEvent { CharacterId = Self, Ready = true });
        connection.Server(new ChallengeAddEvent { Challenge = new ChallengeInformation { State = ChallengeState.Running, ChallengeId = challenge, Bonus = 100 } });
        connection.Server(new FightStartEvent());
        connection.Server(new FightNewRoundEvent { Round = 1 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());

        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Cast(connection, monster, apLeft: 3, actionId: 1);
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));
        Cast(connection, monster, apLeft: 0, actionId: 2);
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
        connection.Server(new FightTurnEndEvent { FighterId = Self });

        if (secondTurn)
        {
            connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 2 });
            connection.Server(new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, 6), Usable(23, 0) } } });
            connection.Server(new FightTurnStartPlayingEvent());
            Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 8));
            Cast(connection, monster, apLeft: 3, actionId: 3);
            Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 9));
            Cast(connection, monster, apLeft: 0, actionId: 4);
            Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 10));
            connection.Server(new FightTurnEndEvent { FighterId = Self });
        }

        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();

        return (connection, sent);
    }

    [Fact]
    public async Task AChallengeATurnLeftIsLeftForTheWholeFight()
    {
        // Zombie with no MP (D466): no turn uses exactly one, so the first leaves it, and the second plays for it no more.
        await ChallengedTurnAsync(TestPlay.ZombieChallenge, ChallengePlay.Easy, secondTurn: true);
        await LoggedAsync($"Bot {Self}: ends its turn after 2 actions");

        Assert.Single(_play.Log.GetSnapshot(), record => record.Message.Contains($"leaves challenge {TestPlay.ZombieChallenge}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnEasyChallengeThatWouldHalveTheTurnsDamageIsLeft()
    {
        // Versatile with one spell (D466): kept, the turn casts it once where it may twice, more than its bonus pays.
        await ChallengedTurnAsync(TestPlay.VersatileChallenge, ChallengePlay.Easy);

        await LoggedAsync($"Bot {Self}: leaves challenge {TestPlay.VersatileChallenge}, which would cost its turn more than its bonus pays");
    }

    [Fact]
    public async Task AnEasyChallengeTheTurnKeepsAnywayIsNotLeft()
    {
        // Prudent, the monster three cells away: the best turn ends beside no enemy.
        await ChallengedTurnAsync(TestPlay.PrudentChallenge, ChallengePlay.Easy);
        await LoggedAsync($"Bot {Self}: ends its turn after 2 actions");

        Assert.DoesNotContain(_play.Log.GetSnapshot(), record => record.Message.Contains("leaves challenge", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AProfileThatPlaysForNoChallengeLeavesNone()
    {
        await ChallengedTurnAsync(TestPlay.VersatileChallenge, ChallengePlay.None);
        await LoggedAsync($"Bot {Self}: ends its turn after 2 actions");

        Assert.DoesNotContain(_play.Log.GetSnapshot(), record => record.Message.Contains("leaves challenge", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACastTheServerRefusedWithATextIsCastAgainTheNextTurn()
    {
        // Reported by players against invisible monsters: a cast refused for a turn (the target not seen) had the AI leave
        // that spell out of the whole fight, and it passed its turns once the monsters were seen again.
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        var refused = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));

        Assert.Equal((TestPlay.BlowSpellId, monster), (refused.SpellId, refused.CellId));
        connection.Server(new TextInformationEvent { TextId = 175, Type = TextInformationType.Error });
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 6));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightTurnEvent { FighterId = Monster, Duration = 300, Round = 1 });
        connection.Server(new FightTurnEndEvent { FighterId = Monster });
        connection.Server(new FightNewRoundEvent { Round = 2 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 2 });
        connection.Server(new FightTurnStartPlayingEvent());

        var again = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 7));

        Assert.Equal((TestPlay.BlowSpellId, monster), (again.SpellId, again.CellId));
    }

    [Fact]
    public async Task ASpellTheServerLeavesUnansweredIsNotCastAgainThatFight()
    {
        // Measured live (D203): a transformed character's own spells go unanswered, on every cell.
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 6));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightTurnEvent { FighterId = Monster, Duration = 300, Round = 1 });
        connection.Server(new FightTurnEndEvent { FighterId = Monster });
        connection.Server(new FightNewRoundEvent { Round = 2 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 2 });
        connection.Server(new FightTurnStartPlayingEvent());

        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
    }

    [Fact]
    public async Task ThePlayersTurnsEndGoesToTheServerAndPausesTheBot()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await StartTurnAsync(connection, bot, monster);

        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        connection.Client(new FightTurnFinishRequest());
        await bot.ProcessedAsync();

        Assert.Empty(connection.Withheld);
        Assert.True(bot.IsPaused);
    }

    [Fact]
    public async Task AWornSwordIsStruckAsSpellZeroBesideTheSpell()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -1, 0);
        var sent = connection.Sent.Count;

        connection.Server(new ObjectAddedEvent { Object = new InventoryObject { Item = new ObjectItem { Uid = 77, ItemId = TestPlay.SwordItemId, Quantity = 1 }, Position = 1 } });
        bot.FightsAlone = true;

        foreach (var message in new IMessage[]
        {
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = TestPlay.MapId },
            new FightStartingEvent { AttackerId = Self, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(Far) },
            new FightFighterShowEvent { Fighter = MonsterActor(monster) },
            new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { monster }, ChallengerCells = { Far, near } } },
            new FightSpellsContextEvent { FighterId = Self, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 }, new SpellItem { SpellId = 0, Grade = 1 } } },
            Order(),
            new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true },
            new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true },
        })
        {
            connection.Server(message);
        }

        var placed = Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 1));

        Assert.Equal(near, placed.CellId);
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = Self, CellId = near } } });
        Assert.IsType<FightReadyRequest>(await SentAsync(connection, sent + 2));
        connection.Server(new FightHumanReadyStateEvent { CharacterId = Self, Ready = true });
        connection.Server(new FightStartEvent());
        connection.Server(new FightNewRoundEvent { Round = 1 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());

        // The sword's blow and the spell, each once, in the order the AI chose; the blow's sequence is a weapon's.
        var spells = new List<int>();

        for (var cast = 1; cast <= 2; cast++)
        {
            var request = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 2 + cast));

            Assert.Equal(monster, request.CellId);
            spells.Add(request.SpellId);

            if (request.SpellId is 0)
                Struck(connection, monster, apLeft: 6 - (3 * cast), actionId: cast);
            else
                Cast(connection, monster, apLeft: 6 - (3 * cast), actionId: cast);
        }

        Assert.Equal([0, TestPlay.BlowSpellId], spells.Order());
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 5));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();
        await LoggedAsync($"Bot {Self}: ends its turn after 2 actions");

        Assert.DoesNotContain(_play.Log.GetSnapshot(), record => record.Message.Contains("did not show", StringComparison.Ordinal));
    }

    private async Task<(TestConnection Connection, int Sent, int Near)> PlacedClosedAsync(ChallengeReward reward, ChallengeBonus? told, bool fights = true)
    {
        // An activity fighting its fights with the profile's choice, the client's own choice answered as the placement starts, then the fight closed.
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);

        // An activity whose AI does not fight plays its fights only through the host's switch.
        bot.FightsAlone = !fights;
        bot.StartActivity(new RewardActivity { Reward = reward, Fights = fights });
        await bot.ProcessedAsync();

        var sent = connection.Sent.Count;

        Placement(connection, near, monster);

        if (told is { } bonus)
            connection.Server(new ChallengeBonusChoiceSelectedEvent { Bonus = bonus });

        Assert.IsType<FightOptionToggleRequest>(await SentAsync(connection, sent + 1));
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });
        Assert.IsType<FightOptionToggleRequest>(await SentAsync(connection, sent + 2));
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });

        return (connection, sent, near);
    }

    [Fact]
    public async Task TheAIChoosesTheProfilesBonusOverTheClientsOwnThenPlaces()
    {
        var (connection, sent, near) = await PlacedClosedAsync(ChallengeReward.Drop, ChallengeBonus.Experience);

        Assert.Equal(ChallengeBonus.Drop, Assert.IsType<ChallengeBonusChoiceRequest>(await SentAsync(connection, sent + 3)).Bonus);
        connection.Server(new ChallengeBonusChoiceSelectedEvent { Bonus = ChallengeBonus.Drop });

        Assert.Equal(near, Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 4)).CellId);
        await LoggedAsync($"Bot {Self}: chooses the challenges' bonus Drop, set");
    }

    [Fact]
    public async Task TheExperienceIsChosenToo()
    {
        var (connection, sent, _) = await PlacedClosedAsync(ChallengeReward.Experience, ChallengeBonus.Drop);

        Assert.Equal(ChallengeBonus.Experience, Assert.IsType<ChallengeBonusChoiceRequest>(await SentAsync(connection, sent + 3)).Bonus);
    }

    [Fact]
    public async Task ABonusTheServerAlreadyGivesIsNotSentAgain()
    {
        var (connection, sent, near) = await PlacedClosedAsync(ChallengeReward.Drop, ChallengeBonus.Drop);

        Assert.Equal(near, Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 3)).CellId);
        Assert.DoesNotContain(connection.Sent, message => message is ChallengeBonusChoiceRequest);
    }

    [Fact]
    public async Task AProfileLeavingTheBonusToTheClientSendsNone()
    {
        var (connection, sent, near) = await PlacedClosedAsync(ChallengeReward.Client, ChallengeBonus.Experience);

        Assert.Equal(near, Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 3)).CellId);
        Assert.DoesNotContain(connection.Sent, message => message is ChallengeBonusChoiceRequest);
    }

    [Fact]
    public async Task ABonusNeverToldNorAnsweredIsSentOnceAndThePlacementGoesOn()
    {
        // The client's own choice never answered, the bot sends its own after its wait; unanswered, it places all the same.
        var (connection, sent, near) = await PlacedClosedAsync(ChallengeReward.Drop, told: null);

        Assert.Equal(ChallengeBonus.Drop, Assert.IsType<ChallengeBonusChoiceRequest>(await SentAsync(connection, sent + 3)).Bonus);
        Assert.Equal(near, Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 4)).CellId);
        await LoggedAsync($"Bot {Self}: chooses the challenges' bonus Drop, unanswered");
        Assert.Single(connection.Sent, message => message is ChallengeBonusChoiceRequest);
    }

    [Fact]
    public async Task AnActivityWhoseAIDoesNotFightSendsNoBonusThoughTheHostsSwitchPlaysTheFight()
    {
        // A harvest whose "The AI fights" is off hides its choice: the host's Fights:Alone plays the fight, the client's choice stays.
        var (connection, sent, near) = await PlacedClosedAsync(ChallengeReward.Drop, ChallengeBonus.Experience, fights: false);

        Assert.Equal(near, Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 3)).CellId);
        Assert.DoesNotContain(connection.Sent, message => message is ChallengeBonusChoiceRequest);
    }

    [Fact]
    public async Task TheClientsOwnChoiceAnsweredLateIsWaitedForBeforeTheBotsIsWeighed()
    {
        // Answered once the fight is closed, the client's choice is the profile's: nothing is sent.
        var (connection, sent, near) = await PlacedClosedAsync(ChallengeReward.Drop, told: null);

        connection.Server(new ChallengeBonusChoiceSelectedEvent { Bonus = ChallengeBonus.Drop });

        Assert.Equal(near, Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 3)).CellId);
        Assert.DoesNotContain(connection.Sent, message => message is ChallengeBonusChoiceRequest);
    }

    [Fact]
    public async Task AFightStartedDuringTheWaitIsSentNoBonus()
    {
        var (connection, sent, _) = await PlacedClosedAsync(ChallengeReward.Drop, told: null);

        Assert.True(_play.Bots.TryGet(TestPlay.Key, out var bot));
        connection.Server(new FightStartEvent());
        await bot.ProcessedAsync();
        _play.Time.Advance(BotDelays.Measured.Answer + BotDelays.Measured.FightAction + BotDelays.Measured.Answer);
        await bot.ProcessedAsync();

        Assert.Equal(sent + 2, connection.Sent.Count);
    }

    [Fact]
    public async Task OutOfAnActivityTheBotLeavesTheFightToThePlayer()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = connection.Sent.Count;

        Placement(connection, Beside(monster, -3, 0), monster);
        await bot.ProcessedAsync();
        _play.Time.Advance(BotDelays.Measured.Display + BotDelays.Measured.FightAction + BotDelays.Measured.Answer);
        await bot.ProcessedAsync();

        Assert.Equal(sent, connection.Sent.Count);
    }

    [Fact]
    public async Task TheAIPlaysTheCharactersCompanionWithTheSpellsTheServerListedForIt()
    {
        // Reported by a player: the Astrub Knight's turns were left to their time. The pack has no companion, so its
        // spells are the server's list of them.
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await CompanionsTurnAsync(connection, bot, monster, listed: true);

        var cast = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 8));

        Assert.Equal((TestPlay.BlowSpellId, monster), (cast.SpellId, cast.CellId));
    }

    [Fact]
    public async Task ACompanionWhoseSpellsTheServerNeverListedEndsItsTurnAtOnce()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = await CompanionsTurnAsync(connection, bot, monster, listed: false);

        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 8));
        Assert.DoesNotContain(connection.Sent.Skip(sent + 7), message => message is GameActionFightCastRequest);
    }

    [Fact]
    public async Task ThePlayersCastPausesTheBotAndTheFightIsThePlayers()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        bot.FightsAlone = true;
        Placement(connection, near, monster);
        await SentAsync(connection, sent + 1);

        // The player's hand during the placement: the bot pauses, and sends nothing more.
        connection.Client(new FightPlacementPositionRequest { CellId = near, FighterId = Self });
        await bot.ProcessedAsync();
        _play.Time.Advance(BotDelays.Measured.Answer + BotDelays.Measured.FightAction);
        await bot.ProcessedAsync();

        Assert.True(bot.IsPaused);
        Assert.Equal(sent + 1, connection.Sent.Count);
    }
}
