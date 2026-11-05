using D3B.Bot.Npcs;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Npcs;

public sealed class NpcTalkTests
{
    private static Task<NpcTalkOutcome> Talk(BotActor bot, int npcId, params int[] replies)
    {
        return bot.RunAsync((context, token) => context.TalkAsync(npcId, replies, token), TestContext.Current.CancellationToken);
    }

    private static NpcDialogQuestionEvent Question(int messageId, params int[] replies)
    {
        var question = new NpcDialogQuestionEvent { MessageId = messageId };

        foreach (var reply in replies)
            question.Replies.Add(new NpcReply { ReplyId = reply });

        return question;
    }

    [Fact]
    public async Task AChainOfRepliesIsGivenOneToEachQuestionUntilTheReplyThatMovesTheCharacter()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var talk = Talk(bot, TestPlay.WayNpcId, TestPlay.WayReplyId, TestPlay.WayLastReplyId);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(30_637, TestPlay.WayReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(Question(30_638, TestPlay.WayLastReplyId, TestPlay.StayReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.WayLastReplyId }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 300);

        Assert.Equal(NpcTalkOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, talk));
        Assert.Equal(TestPlay.NorthMapId, bot.State.MapId);
        Assert.Equal(3, connection.Sent.Count);
    }

    [Fact]
    public async Task AReplyTheQuestionDoesNotOfferLeavesTheDialogAndIsRefused()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var talk = Talk(bot, TestPlay.WayNpcId, TestPlay.WayLastReplyId);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(30_637, TestPlay.WayReplyId));

        Assert.IsType<DialogLeaveRequest>(await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(NpcTalkOutcome.Refused, await TestWalks.AdvanceUntilDoneAsync(play.Time, talk));
        Assert.Equal(TestPlay.InsideMapId, bot.State.MapId);
    }

    [Fact]
    public async Task AChainWithNoReplyOpensTheDialogAndLeavesIt()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var talk = Talk(bot, TestPlay.WayNpcId);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(30_637, TestPlay.WayReplyId));

        Assert.IsType<DialogLeaveRequest>(await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(NpcTalkOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, talk));
        Assert.False(bot.State.IsInDialog);
    }

    [Fact]
    public async Task ADialogThatEndsWithoutMovingTheCharacterIsDoneOnceTheMoveWindowPassed()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var talk = Talk(bot, TestPlay.WayNpcId, TestPlay.WayReplyId);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(30_637, TestPlay.WayReplyId));

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(NpcTalkOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, talk));
        Assert.Equal(TestPlay.InsideMapId, bot.State.MapId);
    }

    [Fact]
    public async Task AnNpcTheMapDoesNotPlaceIsMissingAndNothingIsSent()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);

        Assert.Equal(NpcTalkOutcome.Missing, await Talk(bot, 999_999));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ATalkTheServerDoesNotAnswerIsOverAfterTheAnswerDelay()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var talk = Talk(bot, TestPlay.WayNpcId, TestPlay.WayReplyId);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        Assert.Equal(NpcTalkOutcome.NoAnswer, await TestWalks.AdvanceUntilDoneAsync(play.Time, talk));
    }

    [Fact]
    public async Task ATalkTheServerRefusesIsRefused()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var talk = Talk(bot, TestPlay.WayNpcId, TestPlay.WayReplyId);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new NpcActionErrorEvent());

        Assert.Equal(NpcTalkOutcome.Refused, await TestWalks.AdvanceUntilDoneAsync(play.Time, talk));
    }
}
