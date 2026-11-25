using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Dungeons;
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

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(TestPlay.OtherInsideMapId, bot.State.MapId);
        Assert.Equal(3, connection.Sent.Count);
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

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
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
    public async Task WithNeitherKeyNorKeyringTheActivityEndsSayingSoBeforeAnyJourney()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(13, TestPlay.KeyringId, 1));

        // The high dungeon is on no keyring: the keyring carried opens nothing.
        var run = bot.StartActivity(new DungeonActivity(new DungeonSettings { Dungeons = [TestPlay.HighDungeonId] }, _play.Tables));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
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

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
    }

    [Fact]
    public async Task WhenNoDungeonOfTheProfileCanBeEnteredTheActivityEndsSayingSo()
    {
        var (connection, bot) = await CarryingAsync(TestPlay.MapId, 300, Stack(12, TestPlay.DungeonKeyId, 1));
        var run = bot.StartActivity(Activity(TestPlay.HighDungeonId, 99));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("No dungeon of the profile can be entered: the character lacks the level or the key of each.", Said(run));
        Assert.Empty(connection.Sent);
    }
}
