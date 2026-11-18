using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Fights;

/// <summary>Where the AI takes each fighter's spells from, as it imagines the fight (D193, D443).</summary>
public sealed class ImaginedFightsTests : IAsyncDisposable
{
    private const long Self = TestPlay.CharacterId;
    private const long Knight = -2;
    private const long Larva = -3;

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static Characteristic Value(int id, long total)
    {
        return new Characteristic { Id = id, Value = new CharacteristicValue { Total = total } };
    }

    private static FightSpellsContextEvent Listed(long fighterId)
    {
        return new FightSpellsContextEvent { CharacterId = Self, FighterId = fighterId, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 } } };
    }

    private static Actor Fighter(long id, FightFighter fighter, int cell)
    {
        fighter.Stats = new FightCharacteristics { Characteristics = { Value(1, 6), Value(23, 3), Value(0, 90) } };
        fighter.Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Challenger };

        return new Actor { Id = id, Disposition = new ActorDisposition { CellId = cell }, Information = new ActorInformation { Fighter = fighter } };
    }

    private static GameActionFightEvent SummonedLarva(int cell)
    {
        var summon = new SummonedFighter
        {
            Entity = new SummonedEntity { Monster = new SummonedMonster { MonsterId = TestPlay.LarvaId, Grade = 1 } },
            Stats = new FightCharacteristics { Summoned = true, SummonerId = Self, Characteristics = { Value(1, 6), Value(0, 40) } },
            Positions = { new FighterSpawn { Alive = true, Team = FightTeam.Challenger, Position = new Actor { Id = Larva, Disposition = new ActorDisposition { CellId = cell } } } },
        };

        return new GameActionFightEvent { ActionId = 181, SourceId = Self, Summons = new FightSummons { Fighters = new SummonedFighters { Summons = { summon } } } };
    }

    private FightKnowledge Imagined(params IMessage[] messages)
    {
        IMessage[] fight =
        [
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = TestPlay.MapId },
            new FightFighterShowEvent { Fighter = Fighter(Self, new FightFighter { Named = new NamedFighter { Character = new FighterCharacter { BreedId = 15, Level = 18 } } }, 300) },
            .. messages,
        ];
        var state = fight.Aggregate(CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character), (current, message) => CharacterEvents.Apply(current, RelayDirection.FromServer, message));

        Assert.True(_play.Maps.TryGet(TestPlay.MapId, out var data));

        var imagined = ImaginedFights.Of(state, FightMap.Of(data), _play.Tables, new FightTactics(_play.Tables).Blows, null);

        Assert.NotNull(imagined);

        return imagined.Value.Knowledge;
    }

    [Fact]
    public void ACompanionCastsTheSpellsTheServerListedForIt()
    {
        // The pack has no companion, such as the Astrub Knight: its spells are the server's list alone.
        var knowledge = Imagined(new FightFighterShowEvent { Fighter = Fighter(Knight, new FightFighter { Companion = new CompanionFighter { MasterId = Self } }, 301) }, Listed(Knight));

        Assert.Equal([TestPlay.BlowSpellId], knowledge.SpellsOf(Knight).Select(level => level.SpellId));
    }

    [Fact]
    public void ACompanionWhoseSpellsWereNeverListedCastsNone()
    {
        var knowledge = Imagined(new FightFighterShowEvent { Fighter = Fighter(Knight, new FightFighter { Companion = new CompanionFighter { MasterId = Self } }, 301) });

        Assert.Empty(knowledge.SpellsOf(Knight));
    }

    [Fact]
    public void ASummonKeepsItsMonstersSpellsThoughTheServerListsOthersForIt()
    {
        // The server lists a summon's spells too (D279); the AI's are its monster's at its grade (D193), and the larva has none.
        var knowledge = Imagined(SummonedLarva(301), Listed(Larva));

        Assert.Empty(knowledge.SpellsOf(Larva));
    }
}
