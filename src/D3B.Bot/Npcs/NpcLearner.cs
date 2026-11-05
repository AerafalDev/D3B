using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using D3B.Bot.Characters;
using D3B.Bot.Network;
using D3B.Data.Banks;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf;

namespace D3B.Bot.Npcs;

/// <summary>Teaches <see cref="KnownNpcs"/> the NPCs of every map a game connection goes through and what their dialogs lead to (D383).</summary>
/// <remarks>
/// <para>
/// The server names an NPC by its id and a dialog by its questions' and replies' ids: it sends no name and no text. A map's
/// information lists the NPCs; the dialog's question lists the replies it offers; a reply, given by the player's client or
/// by D3B, is followed by another question, by the dialog's end, by a new map or by an exchange. Measured (D253, D254): a
/// reply's end and the new map come in the same millisecond, 29 ms after the reply, and a portal talked to by a character
/// whose level lets it through moves it before any question.
/// </para>
/// <para>
/// A reply is counted with what follows within <see cref="Window"/>; one the server leaves without an answer, such as a
/// reply it did not offer, counts for nothing. The end of a dialog waits that long for a map change before it is counted.
/// It sees the frames on the relay's read loops: what it keeps of a connection is that connection's alone, and goes with it.
/// </para>
/// </remarks>
internal sealed class NpcLearner : IGameObserver
{
    /// <summary>How long after a reply, or after the dialog it ended, an outcome is counted.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private readonly KnownNpcs _npcs;
    private readonly TimeProvider _time;
    private readonly ConditionalWeakTable<GameSession, Presence> _presences = [];

    public NpcLearner(KnownNpcs npcs, TimeProvider time)
    {
        _npcs = npcs;
        _time = time;
    }

    private static void Request(Presence presence, IMessage message, DateTimeOffset now)
    {
        switch (message)
        {
            case NpcActionRequest { Action: Banker.TalkAction } talk when presence.MapId == talk.MapId && presence.Npcs.TryGetValue(talk.NpcActorId, out var npcId):
                presence.Pending = new Pending(npcId, KnownNpcs.Talk, KnownNpcs.Talk, now);
                break;
            case NpcDialogReplyRequest reply when presence.Dialog is { NpcId: { } npcId, MessageId: { } messageId } dialog && dialog.Replies.Contains(reply.ReplyId):
                presence.Pending = new Pending(npcId, messageId, reply.ReplyId, now);
                break;
        }
    }

    public void OnSessionStarted(GameSession session)
    {
    }

    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (!frame.IsRead)
            return;

        Learn(session, frame.Content, direction is not RelayDirection.FromServer);
    }

    public void OnSent(GameSession session, DecodedGameFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.IsRead)
            Learn(session, frame.Content, true);
    }

    private void Learn(GameSession session, IMessage message, bool isRequest)
    {
        var presence = _presences.GetOrCreateValue(session);
        var now = _time.GetUtcNow();

        lock (presence)
        {
            Expire(presence, now);

            if (isRequest)
                Request(presence, message, now);
            else
                Event(presence, message, now);
        }
    }

    private void Event(Presence presence, IMessage message, DateTimeOffset now)
    {
        switch (message)
        {
            case MapCurrentEvent current:
                Answer(presence, NpcOutcomeKind.MapChange, current.MapId, now);
                presence.MapId = current.MapId;
                presence.Npcs.Clear();
                presence.Dialog = null;
                break;
            case MapInformationEvent information when information.MapId == presence.MapId:
                presence.Npcs.Clear();
                See(presence, MapNpc.In(information.Actors), now);
                break;
            case MapActorShownEvent shown when presence.MapId is not null:
                See(presence, MapNpc.In(shown.Actors), now);
                break;
            case NpcActionErrorEvent when presence.Pending is { ReplyId: KnownNpcs.Talk }:
                presence.Pending = null;
                break;
            case NpcDialogCreationEvent creation when creation.MapId == presence.MapId:
                presence.Dialog = new Dialog(presence.Npcs.TryGetValue(creation.NpcActorId, out var npcId) ? npcId : null);
                break;
            case NpcDialogQuestionEvent question when presence.Dialog is { } dialog:
                Ask(presence, dialog, question, now);
                break;
            case DialogLeaveEvent { Type: DialogType.Npc }:
                presence.Dialog = null;

                if (presence.Pending is { LeftAt: null } pending)
                    presence.Pending = pending with { LeftAt = now };

                break;
            case DialogLeaveEvent:
                presence.Dialog = null;
                break;
            case ExchangeStartedWithStorageEvent started:
                Answer(presence, NpcOutcomeKind.Exchange, (long)started.Type, now);
                break;
            case ExchangeStartedWithTabbedStorageEvent started:
                Answer(presence, NpcOutcomeKind.Exchange, (long)started.Type, now);
                break;
        }
    }

    private void See(Presence presence, ImmutableArray<MapNpc> npcs, DateTimeOffset now)
    {
        if (presence.MapId is not { } mapId)
            return;

        foreach (var npc in npcs)
        {
            presence.Npcs[npc.ActorId] = npc.NpcId;
            _npcs.Saw(mapId, npc.ActorId, npc.NpcId, npc.Cell, npc.Direction, now);
        }
    }

    private void Ask(Presence presence, Dialog dialog, NpcDialogQuestionEvent question, DateTimeOffset now)
    {
        ImmutableArray<int> replies = [.. question.Replies.Select(reply => reply.ReplyId)];

        Answer(presence, NpcOutcomeKind.Question, question.MessageId, now);

        if (presence.MapId is { } mapId && dialog.NpcId is { } npcId)
            _npcs.Asked(mapId, npcId, question.MessageId, replies, !dialog.Asked, now);

        dialog.Asked = true;
        dialog.MessageId = question.MessageId;
        dialog.Replies = replies;
    }

    private void Answer(Presence presence, NpcOutcomeKind kind, long target, DateTimeOffset now)
    {
        if (presence.Pending is not { } pending)
            return;

        presence.Pending = null;

        if (presence.MapId is { } mapId)
            _npcs.Answered(mapId, pending.NpcId, pending.MessageId, pending.ReplyId, kind, target, now);
    }

    private void Expire(Presence presence, DateTimeOffset now)
    {
        if (presence.Pending is not { } pending)
            return;

        if (pending.LeftAt is { } left)
        {
            if (now - left <= Window)
                return;

            presence.Pending = null;

            if (presence.MapId is { } mapId)
                _npcs.Answered(mapId, pending.NpcId, pending.MessageId, pending.ReplyId, NpcOutcomeKind.End, 0, left);
        }
        else if (now - pending.At > Window)
        {
            presence.Pending = null;
        }
    }

    private sealed record Pending(int NpcId, int MessageId, int ReplyId, DateTimeOffset At)
    {
        public DateTimeOffset? LeftAt { get; init; }
    }

    private sealed class Dialog
    {
        public Dialog(int? npcId)
        {
            NpcId = npcId;
        }

        public int? NpcId { get; }

        public bool Asked { get; set; }

        public int? MessageId { get; set; }

        public ImmutableArray<int> Replies { get; set; } = [];
    }

    private sealed class Presence
    {
        public long? MapId { get; set; }

        public Dictionary<int, int> Npcs { get; } = [];

        public Dialog? Dialog { get; set; }

        public Pending? Pending { get; set; }
    }
}
