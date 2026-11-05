using D3B.Bot.Npcs;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Npcs;

public sealed class NpcLearnerTests : IAsyncDisposable
{
    private const long ArrivalMapId = 192_416_776;
    private const int PortalQuestion = 30_637;
    private const int PortalSecondQuestion = 30_638;

    private readonly KnownNpcs _known = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly TestPlay _play;

    public NpcLearnerTests()
    {
        _play = new TestPlay([new NpcLearner(_known, _time)]);
    }

    public async ValueTask DisposeAsync()
    {
        await _play.DisposeAsync();
    }

    private static Actor Npc(int actorId, int npcId, int? cell, ActorDirection direction)
    {
        var actor = new Actor
        {
            Id = actorId,
            Disposition = new ActorDisposition { Direction = direction },
            Information = new ActorInformation { Roleplay = new RoleplayActor { Npc = new NpcActor { NpcId = npcId } } },
        };

        if (cell is { } at)
            actor.Disposition.CellId = at;

        return actor;
    }

    private static NpcDialogQuestionEvent Question(int messageId, params int[] replies)
    {
        var question = new NpcDialogQuestionEvent { MessageId = messageId };

        foreach (var reply in replies)
            question.Replies.Add(new NpcReply { ReplyId = reply });

        return question;
    }

    private TestConnection Inside()
    {
        var connection = _play.Connect(TestPlay.Account);
        var information = TestPlay.Information(396, TestPlay.InsideMapId);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.InsideMapId });
        connection.Server(information);

        return connection;
    }

    private static void Talk(TestConnection connection)
    {
        connection.Client(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
    }

    private KnownNpc Way()
    {
        return Assert.Single(_known.OnMap(TestPlay.InsideMapId), known => known.Npc.NpcId == TestPlay.WayNpcId);
    }

    [Fact]
    public void AMapsInformationTeachesItsNpcsWithTheirCellsAndTheWayTheyFace()
    {
        var connection = _play.Connect(TestPlay.Account);
        var information = new MapInformationEvent { MapId = TestPlay.MapId };

        information.Actors.Add(Npc(-7, 4_398, 300, ActorDirection.Value3));
        information.Actors.Add(Npc(-8, 4_399, null, ActorDirection.Value0));
        information.Actors.Add(new Actor { Id = -1, Disposition = new ActorDisposition { CellId = 101 } });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(information);
        _time.Advance(TimeSpan.FromMinutes(1));
        connection.Server(information);

        var known = _known.OnMap(TestPlay.MapId);

        Assert.Equal([4_398, 4_399], known.Select(npc => npc.Npc.NpcId));
        Assert.Equal((-7, 300, 3, 2), (known[0].Npc.ActorId, known[0].Npc.Cell, known[0].Npc.Direction, known[0].Npc.Seen));
        Assert.Null(known[1].Npc.Cell);
        Assert.Equal((_time.GetUtcNow() - TimeSpan.FromMinutes(1), _time.GetUtcNow()), (known[0].Npc.FirstSeen, known[0].Npc.LastSeen));
        Assert.Empty(_known.OnMap(TestPlay.NorthMapId));
    }

    [Fact]
    public void AnNpcWhoseActorIdChangesStaysOneRowWithTheLastActorId()
    {
        var connection = _play.Connect(TestPlay.Account);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });

        foreach (var actorId in new[] { -7, -9 })
        {
            var information = new MapInformationEvent { MapId = TestPlay.MapId };

            information.Actors.Add(Npc(actorId, 4_398, 300, ActorDirection.Value0));
            connection.Server(information);
        }

        Assert.Equal((-9, 2), (Assert.Single(_known.OnMap(TestPlay.MapId)).Npc.ActorId, _known.OnMap(TestPlay.MapId)[0].Npc.Seen));
    }

    [Fact]
    public void ADialogsQuestionsAndRepliesAreLearnedWithWhatEachLeadsTo()
    {
        var connection = Inside();

        Talk(connection);
        connection.Server(Question(PortalQuestion, TestPlay.WayReplyId));
        connection.Client(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId });
        connection.Server(Question(PortalSecondQuestion, TestPlay.WayLastReplyId, TestPlay.StayReplyId));
        connection.Client(new NpcDialogReplyRequest { ReplyId = TestPlay.WayLastReplyId });
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new MapCurrentEvent { MapId = ArrivalMapId });

        var way = Way();

        Assert.Equal([(PortalQuestion, 1, [TestPlay.WayReplyId]), (PortalSecondQuestion, 0, [TestPlay.WayLastReplyId, TestPlay.StayReplyId])], way.Questions.Select(question => (question.MessageId, question.Opened, question.Replies.ToArray())));
        Assert.Equal(
            [
                (KnownNpcs.Talk, KnownNpcs.Talk, NpcOutcomeKind.Question, (long)PortalQuestion),
                (PortalQuestion, TestPlay.WayReplyId, NpcOutcomeKind.Question, PortalSecondQuestion),
                (PortalSecondQuestion, TestPlay.WayLastReplyId, NpcOutcomeKind.MapChange, ArrivalMapId),
            ],
            way.Outcomes.Select(outcome => (outcome.MessageId, outcome.ReplyId, outcome.Kind, outcome.Target)));
    }

    [Fact]
    public void ADialogEndedByAReplyIsCountedOnceNoMapChangeFollowsWithinASecond()
    {
        var connection = Inside();

        Talk(connection);
        connection.Server(Question(PortalSecondQuestion, TestPlay.WayLastReplyId, TestPlay.StayReplyId));
        connection.Client(new NpcDialogReplyRequest { ReplyId = TestPlay.StayReplyId });
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.DoesNotContain(Way().Outcomes, outcome => outcome.Kind is NpcOutcomeKind.End);

        _time.Advance(NpcLearner.Window + TimeSpan.FromMilliseconds(1));
        connection.Server(new InventoryWeightEvent());

        Assert.Contains(Way().Outcomes, outcome => outcome is { ReplyId: TestPlay.StayReplyId, Kind: NpcOutcomeKind.End, Target: 0, Count: 1 });
        Assert.DoesNotContain(Way().Outcomes, outcome => outcome.Kind is NpcOutcomeKind.MapChange);
    }

    [Fact]
    public void ANpcWhoseTalkMovesTheCharacterAtOnceLeadsToTheMapItMovesTo()
    {
        var connection = Inside();

        connection.Client(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        _time.Advance(TimeSpan.FromMilliseconds(29));
        connection.Server(new MapCurrentEvent { MapId = ArrivalMapId });

        // The dialog the server then opens on the new map names an NPC the new map's information has not listed yet.
        connection.Server(new NpcDialogCreationEvent { MapId = ArrivalMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(30_636));

        Assert.Equal([(KnownNpcs.Talk, KnownNpcs.Talk, NpcOutcomeKind.MapChange, ArrivalMapId)], Way().Outcomes.Select(outcome => (outcome.MessageId, outcome.ReplyId, outcome.Kind, outcome.Target)));
        Assert.Equal(0, _known.QuestionCount);
        Assert.Empty(_known.OnMap(ArrivalMapId));
    }

    [Fact]
    public void ABankersReplyThatOpensTheStorageLeadsToAnExchange()
    {
        var connection = Inside();

        connection.Client(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(Question(26_002, TestPlay.ChestReplyId, TestPlay.OtherReplyId));
        connection.Client(new NpcDialogReplyRequest { ReplyId = TestPlay.ChestReplyId });
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage });

        var banker = Assert.Single(_known.OnMap(TestPlay.InsideMapId), known => known.Npc.NpcId == TestPlay.BankerNpcId);

        Assert.Contains(banker.Outcomes, outcome => outcome is { ReplyId: TestPlay.ChestReplyId, Kind: NpcOutcomeKind.Exchange } && outcome.Target == (long)ExchangeType.Storage);
        Assert.DoesNotContain(banker.Outcomes, outcome => outcome.Kind is NpcOutcomeKind.End);
    }

    [Fact]
    public void AReplyTheQuestionDidNotOfferLeadsToNothing()
    {
        var connection = Inside();

        Talk(connection);
        connection.Server(Question(PortalQuestion, TestPlay.WayReplyId));
        connection.Client(new NpcDialogReplyRequest { ReplyId = TestPlay.StayReplyId });
        connection.Server(Question(PortalQuestion, TestPlay.WayReplyId));

        var way = Way();

        Assert.Equal([(KnownNpcs.Talk, KnownNpcs.Talk)], way.Outcomes.Select(outcome => (outcome.MessageId, outcome.ReplyId)));
        Assert.Equal((1, 2), (Assert.Single(way.Questions).Opened, way.Questions[0].Seen));
    }

    [Fact]
    public void AReplyTheServerNeverAnswersIsDroppedAndTheNextOutcomeIsNotItsOwn()
    {
        var connection = Inside();

        Talk(connection);
        connection.Server(Question(PortalQuestion, TestPlay.WayReplyId));
        connection.Client(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId });
        _time.Advance(TimeSpan.FromSeconds(5));
        connection.Server(Question(PortalSecondQuestion, TestPlay.WayLastReplyId));

        Assert.DoesNotContain(Way().Outcomes, outcome => outcome.ReplyId == TestPlay.WayReplyId);
    }

    [Fact]
    public async Task TheRequestsD3BSendsAreLearnedAsTheClientsAre()
    {
        var connection = Inside();

        await connection.Session.SendAsync(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, TestContext.Current.CancellationToken);
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(Question(PortalQuestion, TestPlay.WayReplyId));
        await connection.Session.SendAsync(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId }, TestContext.Current.CancellationToken);
        connection.Server(Question(PortalSecondQuestion, TestPlay.WayLastReplyId));

        Assert.Equal(
            [(KnownNpcs.Talk, KnownNpcs.Talk, (long)PortalQuestion), (PortalQuestion, TestPlay.WayReplyId, PortalSecondQuestion)],
            Way().Outcomes.Select(outcome => (outcome.MessageId, outcome.ReplyId, outcome.Target)));
    }

    [Fact]
    public void TwoConnectionsKeepTheirDialogsApart()
    {
        var first = Inside();
        var second = Inside();

        Talk(first);
        first.Server(Question(PortalQuestion, TestPlay.WayReplyId));
        second.Client(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId });
        second.Server(Question(PortalSecondQuestion, TestPlay.WayLastReplyId));

        Assert.Equal([(KnownNpcs.Talk, KnownNpcs.Talk)], Way().Outcomes.Select(outcome => (outcome.MessageId, outcome.ReplyId)));
    }
}
