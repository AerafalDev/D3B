using D3B.Bot.Characters;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using System.Text.Json;
using Google.Protobuf;

namespace D3B.Bot.Tests.Characters;

public sealed class CharacterEventsTests
{
    private static readonly CharacterState _chosen = CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character);
    private static readonly JsonSerializerOptions _snapshot = new() { WriteIndented = true };

    private static CharacterState Apply(CharacterState state, params (RelayDirection Side, IMessage Message)[] messages)
    {
        return messages.Aggregate(state, (current, message) => CharacterEvents.Apply(current, message.Side, message.Message));
    }

    private static (RelayDirection, IMessage) Server(IMessage message)
    {
        return (RelayDirection.FromServer, message);
    }

    private static (RelayDirection, IMessage) Client(IMessage message)
    {
        return (RelayDirection.FromClient, message);
    }

    /// <summary>What the server sends on entering the game, in the order of the captures (client.md).</summary>
    private static (RelayDirection, IMessage)[] EnteringTheGame()
    {
        return
        [
            Server(new InventoryContentEvent
            {
                Kamas = 12_500,
                Objects =
                {
                    new InventoryObject { Item = new ObjectItem { Uid = 30, ItemId = 2_400, Quantity = 1 }, Position = 1 },
                    new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = 303, Quantity = 40 }, Position = 63 },
                },
            }),
            Server(new InventoryWeightEvent { Weight = 770, MaxWeight = 7_590 }),
            Server(new JobDescriptionEvent { Jobs = { new JobDescription { JobId = 2, Skills = { new JobSkill { SkillId = 6 }, new JobSkill { SkillId = 39 } } } } }),
            Server(new JobExperiencesEvent
            {
                Jobs = { new JobExperience { JobId = 2, Level = 111, Experience = 123_518, LevelFloor = 122_100, NextLevelFloor = 124_320 } },
            }),
            Client(new ContextCreationRequest()),
            Server(new MapCurrentEvent { MapId = TestPlay.MapId }),
            Client(new MapInformationRequest { MapId = TestPlay.MapId }),
            Server(TestPlay.Information(300)),
        ];
    }

    [Fact]
    public void EnteringTheGameGivesTheCharactersState()
    {
        Assert.Equal(
            """
            {
              "Key": {
                "AccountId": 7,
                "CharacterId": 70001
              },
              "Name": "Character",
              "Level": 200,
              "MapId": 191104002,
              "SubAreaId": 440,
              "Cell": 300,
              "Path": [],
              "IsWalking": false,
              "Inventory": {
                "12": {
                  "Uid": 12,
                  "ItemId": 303,
                  "Quantity": 40,
                  "Position": 63
                },
                "30": {
                  "Uid": 30,
                  "ItemId": 2400,
                  "Quantity": 1,
                  "Position": 1
                }
              },
              "Kamas": 12500,
              "Weight": 770,
              "MaxWeight": 7590,
              "Jobs": {
                "2": {
                  "JobId": 2,
                  "Level": 111,
                  "Experience": 123518,
                  "LevelFloor": 122100,
                  "NextLevelFloor": 124320,
                  "Skills": [
                    6,
                    39
                  ]
                }
              },
              "SubscriptionEnd": null
            }
            """,
            JsonSerializer.Serialize(Apply(_chosen, EnteringTheGame()), _snapshot),
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AChosenCharacterIsKnownByItsNameAndLevelOnly()
    {
        Assert.Equal("Character", _chosen.Name);
        Assert.Equal(200, _chosen.Level);
        Assert.Null(_chosen.MapId);
        Assert.Null(_chosen.Cell);
        Assert.Empty(_chosen.Inventory);
    }

    [Fact]
    public void AWalkEndsOnItsLastCellOnceTheServerAnsweredTheConfirmation()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var walking = Apply(standing, Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } }));

        Assert.True(walking.IsWalking);
        Assert.Equal(300, walking.Cell);

        var confirmed = Apply(walking, Client(new MapMovementConfirmRequest()));

        Assert.True(confirmed.IsWalking);

        var arrived = Apply(confirmed, Server(new MapMovementConfirmResponse()));

        Assert.False(arrived.IsWalking);
        Assert.Equal(302, arrived.Cell);
    }

    [Fact]
    public void AnotherActorsWalkLeavesTheCharacterWhereItIs()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.Same(standing, Apply(standing, Server(new MapMovementEvent { ActorId = 42, Cells = { 300, 301 } })));
    }

    [Fact]
    public void ARefusedPathLeavesTheCharacterOnTheCellTheServerNames()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var point = MapGeometry.PointOf(388);
        var refused = Apply(standing, Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y }));

        Assert.Equal(388, refused.Cell);
        Assert.False(refused.IsWalking);
    }

    [Fact]
    public void AWalkThePlayerStopsLeavesTheCharacterOnTheCellTheClientNames()
    {
        var walking = Apply(_chosen, [.. EnteringTheGame(), Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302, 303 } })]);
        var stopped = Apply(walking, Client(new MapMovementCancelRequest { CellId = 301 }));

        Assert.Equal(301, stopped.Cell);
        Assert.False(stopped.IsWalking);
    }

    [Fact]
    public void ANewMapForgetsTheCellUntilItsInformationPlacesTheCharacter()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var moved = Apply(standing, Server(new MapCurrentEvent { MapId = 88_212_481 }));

        Assert.Equal(88_212_481, moved.MapId);
        Assert.Null(moved.Cell);
        Assert.Null(moved.SubAreaId);

        // The information of another map than the current one places no one.
        Assert.Same(moved, Apply(moved, Server(TestPlay.Information(120))));
        Assert.Equal(120, Apply(moved, Server(TestPlay.Information(120, 88_212_481))).Cell);
    }

    [Fact]
    public void TheInventoryFollowsTheServer()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var changed = Apply(
            standing,
            Server(new ObjectAddedEvent { Object = new InventoryObject { Item = new ObjectItem { Uid = 50, ItemId = 1_844, Quantity = 14 }, Position = 63 } }),
            Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 12, Quantity = 54 } }),
            Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 999, Quantity = 1 } }),
            Server(new ObjectsDeletedEvent { Uids = { 30 } }),
            Server(new KamasUpdateEvent { Kamas = 11_000 }),
            Server(new InventoryWeightEvent { Weight = 812, MaxWeight = 7_590 }));

        Assert.Equal(
            [new InventoryItem(12, 303, 54, 63), new InventoryItem(50, 1_844, 14, 63)],
            changed.Inventory.Values);
        Assert.Equal(11_000, changed.Kamas);
        Assert.Equal(812, changed.Weight);
    }

    [Fact]
    public void AProfessionsNewExperienceKeepsItsSkills()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var gained = Apply(standing, Server(new JobExperiencesEvent { Jobs = { new JobExperience { JobId = 2, Level = 112, Experience = 124_400 } } }));
        var job = gained.Jobs[2];

        Assert.Equal(112, job.Level);
        Assert.Equal([6, 39], job.Skills);
    }

    [Fact]
    public void AMessageFromTheOtherSideChangesNothing()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.Same(standing, Apply(standing, Server(new MapMovementCancelRequest { CellId = 10 })));
        Assert.Same(standing, Apply(standing, Client(new MapCurrentEvent { MapId = 1 })));
    }

    [Fact]
    public void ACellOffTheMapIsNotTaken()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.Same(standing, Apply(standing, Client(new MapMovementCancelRequest { CellId = 560 })));
        Assert.Same(standing, Apply(standing, Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 900 } })));
    }
}
