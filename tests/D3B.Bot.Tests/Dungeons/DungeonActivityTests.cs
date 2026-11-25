using System.Collections.Immutable;
using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Dungeons;
using D3B.Bot.Fights;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Dungeons;

/// <summary>The dungeon activity (D520): the key checked, the door reached, the keeper talked to by its replies' kinds (D522, D523).</summary>
public sealed class DungeonActivityTests : IAsyncDisposable
{
    private const int KeeperQuestion = 13_769;
    private const int ConfirmQuestion = 17_460;
    private const long GroupId = -3;
    private const int ExitNpcId = 780;
    private const int ExitActorId = -20_005;
    private const int GroupCell = 412;

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static InventoryObject Stack(int uid, int itemId, int quantity)
    {
        return new InventoryObject { Item = new ObjectItem { Uid = uid, ItemId = itemId, Quantity = quantity }, Position = InventoryItem.NotWorn };
    }

    private static NpcDialogQuestionEvent Question(int messageId, params int[] replies)
    {
        var question = new NpcDialogQuestionEvent { MessageId = messageId };

        foreach (var reply in replies)
            question.Replies.Add(new NpcReply { ReplyId = reply });

        return question;
    }

    private static Actor GroupActor(long actorId, int cell, int monsterId)
    {
        var group = new MonsterGroup { Main = new GroupMonster { MonsterId = monsterId, Grade = 1, Level = 20 } };

        return new Actor
        {
            Id = actorId,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation { Roleplay = new RoleplayActor { MonsterGroup = new MonsterGroupActor { Monsters = group } } },
        };
    }

    private static FightEndEvent Ended(FightOutcome outcome)
    {
        return new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = TestPlay.CharacterId }, Outcome = outcome } } };
    }

    // Measured (9g): the fight's context replaces the map's, and the map's comes back with the end.
    private static void Fought(TestConnection connection, FightOutcome outcome)
    {
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(Ended(outcome));
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
    }

    private async Task<(TestConnection Connection, BotActor Bot)> InARoomAsync(long mapId, bool group, params InventoryObject[] stacks)
    {
        var (connection, bot) = await CarryingAsync(mapId, 300, stacks);

        if (group)
        {
            connection.Server(new MapActorShownEvent { Actors = { GroupActor(GroupId, GroupCell, TestPlay.LarvaId) } });
            await bot.ProcessedAsync();
        }

        return (connection, bot);
    }

    private static string Said(ActivityRun run)
    {
        var reason = Assert.IsType<ActivityException>(run.Failure).Reason!;
        var english = CultureInfo.GetCultureInfo("en");

        return string.Format(english, reason.Format.In(english), [.. reason.Values.Select(value => (object)value.Id)]);
    }

    private async Task<(TestConnection Connection, BotActor Bot)> CarryingAsync(long mapId, int cell, params InventoryObject[] stacks)
    {
        var (connection, bot) = await _play.EnterAsync(cell, mapId);
        var inventory = new InventoryContentEvent();

        inventory.Objects.AddRange(stacks);
        connection.Server(inventory);
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    // The server places a map's NPCs with its information.
    private static void ShowExitNpc(TestConnection connection, long mapId)
    {
        var information = TestPlay.Information(300, mapId);

        information.Actors.Add(new Actor
        {
            Id = ExitActorId,
            Disposition = new ActorDisposition { CellId = 302 },
            Information = new ActorInformation { Roleplay = new RoleplayActor { Npc = new NpcActor { NpcId = ExitNpcId } } },
        });
        connection.Server(information);
    }

    // Measured at Champs: question 13770, reply 15920 "Sortir." among others, the dialog's end, then the exit map (client-game.md).
    private async Task ExitedAsync(TestConnection connection, long mapId, int sent, bool confirmed = false)
    {
        connection.Server(new NpcDialogCreationEvent { MapId = mapId, NpcActorId = ExitActorId });
        connection.Server(Question(13_770, TestPlay.StayReplyId, TestPlay.DungeonExitReplyId));
        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.DungeonExitReplyId }, await TestWalks.SentAsync(_play, connection, sent));

        // An exit that asks again is confirmed by a keeper's "Oui." (D526).
        if (confirmed)
        {
            connection.Server(Question(17_461, TestPlay.StayReplyId, TestPlay.ConfirmReplyId));
            Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ConfirmReplyId }, await TestWalks.SentAsync(_play, connection, sent + 1));
        }

        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 300);
    }

    private DungeonActivity Runs(int runs, params int[] dungeons)
    {
        return new DungeonActivity(new DungeonSettings { Dungeons = dungeons, Runs = runs, RestartAfterFailure = false }, _play.Tables);
    }

    private DungeonActivity Activity(params int[] dungeons)
    {
        return new DungeonActivity(new DungeonSettings { Dungeons = dungeons.Length > 0 ? dungeons : [TestPlay.DungeonId], RestartAfterFailure = false }, _play.Tables);
    }

    [Fact]
    public async Task AtTheDoorTheKeeperIsGivenTheKeyAndTheEntryConfirmedTheBankerLeftAlone()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity());

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.KeyReplyId, TestPlay.StayReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.KeyReplyId }, await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(Question(ConfirmQuestion, TestPlay.ConfirmReplyId, TestPlay.StayReplyId));

        // Measured: the dialog's end, the key spent and the first room in the same milliseconds.
        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ConfirmReplyId }, await TestWalks.SentAsync(_play, connection, 3));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ObjectDeletedEvent { Uid = 12 });
        TestWalks.Arrive(connection, TestPlay.OtherInsideMapId, 300);
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.OtherInsideMapId, bot.State.MapId);
        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task TheKeyringIsChosenWhenTheKeeperOffersItSoThatNoKeyIsSpent()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(12, TestPlay.DungeonKeyId, 1), Stack(13, TestPlay.KeyringId, 1));
        var run = bot.StartActivity(Activity());

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.KeyReplyId, TestPlay.KeyringReplyId, TestPlay.StayReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.KeyringReplyId }, await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(Question(ConfirmQuestion, TestPlay.ConfirmReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ConfirmReplyId }, await TestWalks.SentAsync(_play, connection, 3));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.OtherInsideMapId, 300);
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.OtherInsideMapId, bot.State.MapId);
        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task TheKeyringAloneOpensADungeonOnIt()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(13, TestPlay.KeyringId, 1));
        var run = bot.StartActivity(Activity());

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.KeyReplyId, TestPlay.KeyringReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.KeyringReplyId }, await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.OtherInsideMapId, 300);
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.OtherInsideMapId, bot.State.MapId);
        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task WithoutTheKeyAndTheKeyringTheActivityEndsSayingSoBeforeAnyJourney()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(14, TestPlay.Ash, 3));
        var run = bot.StartActivity(Activity());

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("The character carries neither the dungeon's key nor a keyring that opens it.", Said(run));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADoorNoWayLeadsToIsLeftForTheNextDungeonOfTheProfile()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));

        // The far dungeon's door is off the graph: three journeys find no path, and its turn goes to the next (D524).
        var run = bot.StartActivity(Activity(TestPlay.FarDungeonId, TestPlay.DungeonId));

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(_play, connection, 1));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task ADoorNoWayLeadsToFailsAProfileOfThatDungeonAloneSoThatARestartMayTryAgain()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300);
        var run = bot.StartActivity(Activity(TestPlay.FarDungeonId));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("No way leads to the dungeon's door from where the character stands.", Said(run));
        Assert.False(Assert.IsType<ActivityException>(run.Failure).IsFinal);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AwayFromTheDoorTheCharacterTravelsThere()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity());

        // The inside is the dungeon's door, through the bank's door of the map outside.
        await TestWalks.WalkedAsync(_play, connection, 1);
        Assert.Equal(new InteractiveUseRequest { ElementId = TestPlay.DoorElementId, SkillInstanceId = TestPlay.DoorInstanceId }, await TestWalks.SentAsync(_play, connection, 2));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AKeyringCarriedOpensNoDungeonOffItsRingAndNoRestartChangesThat()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(13, TestPlay.KeyringId, 1));

        // The other dungeon is on no keyring: the keyring carried opens nothing, its profile's restart on (D519).
        var run = bot.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.OtherDungeonId] }, _play.Tables));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("The character carries neither the dungeon's key nor a keyring that opens it.", Said(run));
        Assert.Equal(0, run.Restarts);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterBelowTheDungeonsLevelIsNotTakenThere()
    {
        // The test's character is level 200, which the high dungeon asks one more than.
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity(TestPlay.HighDungeonId));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("The character is level 200, and the dungeon lets in from level 201.", Said(run));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADungeonTheClientLacksEndsTheActivity()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity(99));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("The dungeon this profile names is not in the client's data.", Said(run));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AKeeperWhoOffersNoReplyOfAKeeperLeavesTheActivityEndedAtTheDoor()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity());

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.WayReplyId, TestPlay.StayReplyId));

        Assert.IsType<DialogLeaveRequest>(await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("No NPC at the dungeon's door let the character in.", Said(run));
        Assert.Equal(TestPlay.InsideMapId, bot.State.MapId);
    }

    [Fact]
    public async Task OfTheProfilesDungeonsTheNextTheCharacterCanEnterIsTheOneItGoesTo()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));

        // The high dungeon comes first and asks a level the character lacks: its turn is left out (D524).
        var run = bot.StartActivity(Activity(TestPlay.HighDungeonId, TestPlay.DungeonId));

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(_play, connection, 1));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task ADungeonItsKeeperTurnsAwayIsLeftForTheNextOfTheProfile()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity(TestPlay.DungeonId, TestPlay.OtherDungeonId));

        // The first dungeon's keeper offers no reply of a keeper: the dungeon is shut for the run (D524).
        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.StayReplyId));
        Assert.IsType<DialogLeaveRequest>(await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        // The next of the profile shares its door: its keeper is talked to again, and lets the character into its room.
        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 3));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.KeyReplyId));
        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.KeyReplyId }, await TestWalks.SentAsync(_play, connection, 4));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.EastMapId, 300);
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task WhenNoDungeonOfTheProfileCanBeEnteredTheActivityEndsSayingSo()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity(TestPlay.HighDungeonId, 99));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("No dungeon of the profile can be entered: the character lacks the level or the key of each, or no way leads to them.", Said(run));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ARoomsGroupIsFoughtAndTheServersMoveLeadsToTheNextRoomsFight()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.OtherInsideMapId, true);
        var run = bot.StartActivity(Activity());

        Assert.Equal(GroupCell, await TestWalks.WalkedAsync(_play, connection, 1));
        Assert.Equal(new AttackMonsterRequest { MonsterGroupId = GroupId }, await connection.SentAsync(2));
        Fought(connection, FightOutcome.Won);

        // Measured: the next room comes in the fight end's millisecond, unasked (client-game.md).
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 300);
        connection.Server(new MapActorShownEvent { Actors = { GroupActor(-4, 300, TestPlay.LarvaId) } });

        // The next room's group stands on the character's cell: it is attacked from there.
        Assert.Equal(new AttackMonsterRequest { MonsterGroupId = -4 }, await TestWalks.SentAsync(_play, connection, 3));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Contains("1 room won", CountsOf(run));
    }

    [Fact]
    public async Task AWonRoomTheServerDidNotLeaveIsLeftOnFootForTheNextRoom()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.OtherInsideMapId, false);
        var run = bot.StartActivity(Activity());

        // No group shows: the room is won, and the room north is walked to, out by the other inside's map action (D525).
        Assert.Equal(TestPlay.OtherActionCell, await TestWalks.WalkedAsync(_play, connection, 1));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task OnceEveryRoomIsWonTheExitIsTheLastRoomsNpc()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.EastMapId, false);

        ShowExitNpc(connection, TestPlay.EastMapId);
        await bot.ProcessedAsync();

        // As Forgerons' (client-game.md): no map after the last fight, the exit's NPC in the last room (D526).
        var run = bot.StartActivity(Runs(1, TestPlay.OtherDungeonId));

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.EastMapId, NpcActorId = ExitActorId }, await TestWalks.SentAsync(_play, connection, 1));
        await ExitedAsync(connection, TestPlay.EastMapId, 2, confirmed: true);

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Contains("1 dungeon done", CountsOf(run));
    }

    [Fact]
    public async Task OnTheMapTheBosssWinLeadsToTheExitsNpcLetsTheCharacterOut()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.EastMapId, true);
        var run = bot.StartActivity(Runs(1, TestPlay.OtherDungeonId));

        await TestWalks.WalkedAsync(_play, connection, 1);
        Assert.IsType<AttackMonsterRequest>(await connection.SentAsync(2));
        Fought(connection, FightOutcome.Won);

        // The first map is of the dungeon's sub-area and no room: the map its boss's win leads to, its NPC the exit's.
        TestWalks.Arrive(connection, TestPlay.MapId, 300);
        ShowExitNpc(connection, TestPlay.MapId);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.MapId, NpcActorId = ExitActorId }, await TestWalks.SentAsync(_play, connection, 3));
        await ExitedAsync(connection, TestPlay.MapId, 4);

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(TestPlay.InsideMapId, bot.State.MapId);
    }

    [Fact]
    public async Task OutOfADungeonTheNextRunEntersTheNextOfTheProfile()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.EastMapId, false, Stack(12, TestPlay.DungeonKeyId, 1));

        ShowExitNpc(connection, TestPlay.EastMapId);
        await bot.ProcessedAsync();

        // No count of runs: the profile goes on, and the exit is the door of the dungeons it lists (D524, D526).
        var run = bot.StartActivity(Activity(TestPlay.OtherDungeonId, TestPlay.DungeonId));

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        await ExitedAsync(connection, TestPlay.EastMapId, 2);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(_play, connection, 3));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task ARunsPodsAreMeasuredSoThatTheCharacterBanksBeforeTheNextRunWouldFillIt()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));

        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        var activity = Runs(1, TestPlay.OtherDungeonId);
        var run = bot.StartActivity(activity);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.KeyReplyId));
        Assert.IsType<NpcDialogReplyRequest>(await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.EastMapId, 300);
        ShowExitNpc(connection, TestPlay.EastMapId);

        // The room shows no group and holds the exit: the run brought 300 pods, measured as the character leaves (D527).
        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 3));
        connection.Server(new InventoryWeightEvent { Weight = 400, MaxWeight = 1_000 });
        await ExitedAsync(connection, TestPlay.EastMapId, 4);

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(300, activity.ExpectedGain());

        // At the 90 % of the bank's share: 650 pods and the next run's 300 reach it, 550 do not.
        var outside = bot.State with { MapId = TestPlay.MapId };

        Assert.True(activity.IsFull(outside with { Weight = 650 }));
        Assert.False(activity.IsFull(outside with { Weight = 550 }));
    }

    [Fact]
    public async Task OnceBankedForTheNextRunTheCharacterEntersItEvenWithWhatTheBankLeft()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 396, Stack(12, TestPlay.DungeonKeyId, 1));

        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        var run = bot.StartActivity(Activity(TestPlay.OtherDungeonId));

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.KeyReplyId));
        Assert.IsType<NpcDialogReplyRequest>(await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.EastMapId, 300);
        ShowExitNpc(connection, TestPlay.EastMapId);

        // The run brings 550 pods: at 650, the next would pass the 90 % share, so the character banks first (D527).
        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 3));
        connection.Server(new InventoryWeightEvent { Weight = 650, MaxWeight = 1_000 });
        await ExitedAsync(connection, TestPlay.EastMapId, 4);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId }, await TestWalks.SentAsync(_play, connection, 5));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { new NpcReply { ReplyId = TestPlay.ChestReplyId } } });
        Assert.IsType<NpcDialogReplyRequest>(await TestWalks.SentAsync(_play, connection, 6));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent());

        // No loot to put in: the 650 pods stay, and the next run is entered rather than the activity ended still full.
        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(_play, connection, 7));
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(_play, connection, 8));
        Assert.False(run.IsOver);

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task FullAtTheBanksShareTheCharacterBanksItsLootKeepingTheKeyThenEnters()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.InsideMapId, 396, Stack(12, TestPlay.Ash, 40), Stack(13, TestPlay.DungeonKeyId, 1));

        connection.Server(new InventoryWeightEvent { Weight = 950, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        // The ash a fight dropped is the activity's loot; the key it carries never is (D526).
        var activity = Activity(TestPlay.OtherDungeonId);

        activity.OnFightOver(new FightEnd(FightOutcome.Won, 0, 0, ImmutableSortedDictionary<int, int>.Empty.Add(TestPlay.Ash, 40)), InterruptionResponse.Resume);

        var run = bot.StartActivity(activity);

        // The door is the bank's inside: the banker is talked to first, as the bank's tests measure.
        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId }, await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { new NpcReply { ReplyId = TestPlay.ChestReplyId } } });
        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ChestReplyId }, await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent());

        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 12 } }, await TestWalks.SentAsync(_play, connection, 3));
        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        connection.Server(new StorageObjectsUpdateEvent());
        connection.Server(new InventoryWeightEvent { Weight = 300, MaxWeight = 1_000 });

        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(_play, connection, 4));
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        // The bank left, the keeper beside it is talked to, the key still carried.
        await _play.AdvanceUntilAsync(() => connection.Sent.OfType<NpcActionRequest>().Any(request => request.NpcActorId == TestPlay.WayActorId), TimeSpan.FromSeconds(1));
        Assert.True(CarriesKey(bot));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public void FullOutsideTheCharacterBanksItsLootButTheKeysAndInsideItNeverBanks()
    {
        var activity = Activity(TestPlay.DungeonId);
        var full = new CharacterState { Key = TestPlay.Key, Name = "Character", Weight = 950, MaxWeight = 1_000, MapId = TestPlay.MapId };

        Assert.True(activity.IsFull(full));
        Assert.False(activity.IsFull(full with { MapId = TestPlay.OtherInsideMapId }));
        Assert.False(activity.IsFull(full with { Weight = 800 }));

        // Its loot, a dropped key among it, banked but the key, which the next runs open the dungeon with (D526).
        activity.OnFightOver(new FightEnd(FightOutcome.Won, 0, 0, ImmutableSortedDictionary<int, int>.Empty.Add(TestPlay.Ash, 3).Add(TestPlay.DungeonKeyId, 1)), InterruptionResponse.Resume);

        Assert.True(activity.Deposits(new InventoryItem(20, TestPlay.Ash, 3, InventoryItem.NotWorn)));
        Assert.False(activity.Deposits(new InventoryItem(21, TestPlay.DungeonKeyId, 2, InventoryItem.NotWorn)));
        Assert.False(activity.Deposits(new InventoryItem(22, TestPlay.Bread, 1, InventoryItem.NotWorn)));
    }

    [Fact]
    public async Task AFightLostLeavesTheDungeonForTheRun()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.OtherInsideMapId, true, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity());

        await TestWalks.WalkedAsync(_play, connection, 1);
        Assert.IsType<AttackMonsterRequest>(await connection.SentAsync(2));
        Fought(connection, FightOutcome.Lost);

        // The character wakes outside, its key still carried: the dungeon is not entered again (D525).
        TestWalks.Arrive(connection, TestPlay.MapId, 300);

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("A fight was lost in the dungeon: it is left for the run.", Said(run));
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task UnderItsShareOfLifeTheCharacterRegeneratesBeforeTheRoomsFight()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.OtherInsideMapId, true);

        connection.Server(new LifeRegenEndEvent { Life = 300, MaxLife = 1_000 });
        await bot.ProcessedAsync();

        var run = bot.StartActivity(Activity());

        // Far under the 80 % of the profile: a minute and more of regeneration, and no attack meanwhile (D525).
        await _play.AdvanceUntilAsync(() => Doing(run).StartsWith("Regenerating its life before the room's fight", StringComparison.Ordinal), TimeSpan.FromSeconds(1));
        _play.Time.Advance(TimeSpan.FromSeconds(30));
        await bot.ProcessedAsync();
        Assert.Empty(connection.Sent);

        connection.Server(new LifeRegenEndEvent { Life = 900, MaxLife = 1_000 });

        Assert.Equal(GroupCell, await TestWalks.WalkedAsync(_play, connection, 1));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task ARoomWonByAFightIsNeverWalkedBackTo()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.NorthMapId, true);
        var run = bot.StartActivity(Activity());

        await TestWalks.WalkedAsync(_play, connection, 1);
        Assert.IsType<AttackMonsterRequest>(await connection.SentAsync(2));
        Fought(connection, FightOutcome.Won);

        // The server moves the character to the other room, which shows no group: with both rooms won, no way back is taken,
        // and the exit is sought in the last room, which holds no NPC.
        TestWalks.Arrive(connection, TestPlay.OtherInsideMapId, 300);

        Assert.Equal(ActivityOutcome.Failed, await TestWalks.AdvanceUntilDoneAsync(_play.Time, run.Completion));
        Assert.Equal("No NPC of the dungeon's last map let the character out.", Said(run));
        Assert.Equal(2, connection.Sent.Count);
        Assert.Contains("2 rooms won", CountsOf(run));
    }

    [Fact]
    public async Task AfterAFightLostTheProfilesOtherDungeonIsStillEntered()
    {
        var (connection, bot) = await InARoomAsync(TestPlay.OtherInsideMapId, true, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity(TestPlay.DungeonId, TestPlay.OtherDungeonId));

        await TestWalks.WalkedAsync(_play, connection, 1);
        Assert.IsType<AttackMonsterRequest>(await connection.SentAsync(2));
        Fought(connection, FightOutcome.Lost);

        // The character wakes at the door of both dungeons: the one lost in is left, the other's keeper talked to (D524, D525).
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 300);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(_play, connection, 3));

        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    private bool CarriesKey(BotActor bot)
    {
        return DungeonActivity.CarriesKey(bot.State, _play.Tables.Dungeons, _play.Tables.Dungeons.Dungeons.Span[2]);
    }

    private static string Doing(ActivityRun run)
    {
        var english = CultureInfo.GetCultureInfo("en");
        var doing = run.Activity.Report!.Doing;

        return string.Format(english, doing.Format.In(english), [.. doing.Values.Select(value => (object)value.Id)]);
    }

    private static string[] CountsOf(ActivityRun run)
    {
        var english = CultureInfo.GetCultureInfo("en");

        // English counts 1 as singular alone, as the app does.
        return [.. run.Activity.Report!.Counts.Select(count => string.Format(english, (count.Text.Count is 1 || count.Text.Many is null ? count.Text.Format : count.Text.Many).In(english), [.. count.Text.Values.Select(value => (object)value.Id)]))];
    }
}
