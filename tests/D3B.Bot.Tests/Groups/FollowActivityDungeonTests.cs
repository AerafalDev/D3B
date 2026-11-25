using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Groups;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

/// <summary>A follower behind its master into a dungeon and out of it, by itself, with its own key (D528).</summary>
public sealed class FollowActivityDungeonTests
{
    private const int KeeperQuestion = 13_769;
    private const int ConfirmQuestion = 17_460;
    private const int ExitNpcId = 780;
    private const int ExitActorId = -20_005;

    private static ActivityRun Follow(TestPlay play, BotActor follower)
    {
        return follower.StartActivity(new FollowActivity(new FollowSettings { Master = TestGroup.Master.ToId() }, play.Bots, play.Tables));
    }

    private static string Doing(ActivityRun run)
    {
        return TestGroup.Say(run.Activity.Report?.Doing);
    }

    private static NpcDialogQuestionEvent Question(int messageId, params int[] replies)
    {
        var question = new NpcDialogQuestionEvent { MessageId = messageId };

        foreach (var reply in replies)
            question.Replies.Add(new NpcReply { ReplyId = reply });

        return question;
    }

    private static async Task CarryAsync(TestConnection connection, BotActor bot, int itemId)
    {
        var inventory = new InventoryContentEvent();

        inventory.Objects.Add(new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = itemId, Quantity = 1 }, Position = InventoryItem.NotWorn });
        connection.Server(inventory);
        await bot.ProcessedAsync();
    }

    [Fact]
    public async Task AFollowerAtTheDoorOfItsMastersDungeonTalksToItsKeeperWithItsOwnKeyAndFollowsItIn()
    {
        // Measured (client-game.md): in a party each character talks to the keeper itself, its key then "Oui." (D528).
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300, TestPlay.OtherInsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.InsideMapId);

        await CarryAsync(connection, follower, TestPlay.DungeonKeyId);

        var leading = master.StartActivity(new DungeonMasterActivity { Inside = TestPlay.DungeonId });
        var run = Follow(play, follower);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestGroup.SentAsync(play, connection, 1));
        Assert.Equal("Enters the dungeon behind Master", Doing(run));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.KeyReplyId, TestPlay.StayReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.KeyReplyId }, await TestGroup.SentAsync(play, connection, 2));
        connection.Server(Question(ConfirmQuestion, TestPlay.ConfirmReplyId, TestPlay.StayReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ConfirmReplyId }, await TestGroup.SentAsync(play, connection, 3));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ObjectDeletedEvent { Uid = 12 });
        TestGroup.Arrive(connection, TestGroup.FollowerId, TestPlay.OtherInsideMapId, 302);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following its master in the dungeon");
        Assert.Equal(TestPlay.OtherInsideMapId, follower.State.MapId);
        run.Stop();
        leading.Stop();
    }

    [Fact]
    public async Task AFollowerAwayFromItsMastersDungeonGoesToItsDoorRatherThanToItsMastersMap()
    {
        // A room has no way in from outside but its keeper (D520): the follower goes to the entrance, not to its master's map.
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300, TestPlay.NorthMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301);

        await CarryAsync(connection, follower, TestPlay.DungeonKeyId);

        var leading = master.StartActivity(new DungeonMasterActivity { Inside = TestPlay.DungeonId });
        var run = Follow(play, follower);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Goes to the dungeon Master is in", "going to the dungeon's door");
        // The door into the entrance, where the master's map is north by a scroll.
        Assert.Equal(TestPlay.DoorElementId, Assert.IsType<InteractiveUseRequest>(await TestGroup.SentAsync(play, connection, 1)).ElementId);
        run.Stop();
        leading.Stop();
    }

    [Fact]
    public async Task AFollowerWithoutTheKeyWaitsAtTheDoorTalkingToNobody()
    {
        // The master leaves out a dungeon a follower cannot enter (D528); one that still cannot, such as its key gone since,
        // waits at the door, and its master goes on without it after five minutes (D490).
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300, TestPlay.OtherInsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.InsideMapId);
        var leading = master.StartActivity(new DungeonMasterActivity { Inside = TestPlay.DungeonId });
        var run = Follow(play, follower);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Waits at the door: no key to enter the dungeon behind Master", "waiting at the door");
        await TestGroup.SettleAsync(follower, master);
        play.Time.Advance(TimeSpan.FromSeconds(30));
        await TestGroup.SettleAsync(follower, master);

        Assert.Empty(connection.Sent);
        run.Stop();
        leading.Stop();
    }

    [Fact]
    public async Task AFollowerTheKeeperTurnedAwayWaitsAtTheDoorRatherThanAskingAgainAndAgain()
    {
        await using var play = new TestPlay();
        var (_, master) = await TestGroup.MasterAsync(play, 300, TestPlay.OtherInsideMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.InsideMapId);

        await CarryAsync(connection, follower, TestPlay.DungeonKeyId);

        var leading = master.StartActivity(new DungeonMasterActivity { Inside = TestPlay.DungeonId });
        var run = Follow(play, follower);

        // The keeper offers no reply that enters, such as for a key the server takes not.
        Assert.IsType<NpcActionRequest>(await TestGroup.SentAsync(play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(KeeperQuestion, TestPlay.StayReplyId));

        await TestGroup.UntilAsync(play, () => Doing(run) == "Waits at the door: the keeper turned it away from the dungeon behind Master", "waiting, turned away");
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        var sent = connection.Sent.Count;

        play.Time.Advance(TimeSpan.FromMinutes(1));
        await TestGroup.SettleAsync(follower, master);

        Assert.DoesNotContain(connection.Sent.Skip(sent), request => request is NpcActionRequest);
        run.Stop();
        leading.Stop();
    }

    [Fact]
    public async Task AFollowerInADungeonItsMasterLeftTalksToTheExitsNpcThenFollowsItOut()
    {
        // Measured (client-game.md): each character of the party left by the exit's NPC itself, "Sortir." then the exit map.
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300, TestPlay.NorthMapId);
        var (connection, follower) = await TestGroup.FollowerAsync(play, 301, TestPlay.NorthMapId);
        var inside = new DungeonMasterActivity { Inside = TestPlay.DungeonId };
        var leading = master.StartActivity(inside);
        var run = Follow(play, follower);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following its master in the dungeon");

        // The server shows the exit's NPC in the last room, as Forgerons' (D526).
        var information = TestPlay.Information(301, TestPlay.NorthMapId, TestGroup.FollowerId);

        information.Actors.Add(new Actor
        {
            Id = ExitActorId,
            Disposition = new ActorDisposition { CellId = 302 },
            Information = new ActorInformation { Roleplay = new RoleplayActor { Npc = new NpcActor { NpcId = ExitNpcId } } },
        });
        connection.Server(information);
        await follower.ProcessedAsync();

        inside.Inside = null;
        TestGroup.Arrive(masterConnection, TestGroup.MasterId, TestPlay.InsideMapId, 300);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.NorthMapId, NpcActorId = ExitActorId }, await TestGroup.SentAsync(play, connection, 1));
        Assert.Equal("Leaves the dungeon behind Master", Doing(run));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.NorthMapId, NpcActorId = ExitActorId });
        connection.Server(Question(13_770, TestPlay.StayReplyId, TestPlay.DungeonExitReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.DungeonExitReplyId }, await TestGroup.SentAsync(play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestGroup.Arrive(connection, TestGroup.FollowerId, TestPlay.InsideMapId, 301);

        await TestGroup.UntilAsync(play, () => Doing(run) == "Follows Master", "following its master out");
        Assert.Equal(TestPlay.InsideMapId, follower.State.MapId);
        run.Stop();
        leading.Stop();
    }
}
