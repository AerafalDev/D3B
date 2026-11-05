using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data.Banks;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Npcs;

/// <summary>Talking to an NPC of the map and giving it a chain of replies: the n-th reply to the n-th question (D384).</summary>
/// <remarks>
/// <para>
/// The same dialog as an NPC's way through the world (D253), measured there (client.md): the NPC is talked to from where the
/// character stands, as a banker is (D80), the server asks its question 28 ms after and each next one 28 to 34 ms after a
/// reply, and the client's display time passes before each reply so that the question is shown before the reply closes it
/// (D132). A reply that ends the dialog and moves the character names the new map in the same millisecond.
/// </para>
/// <para>
/// A chain ends the dialog: a question left asking after the last reply is left, the way the client's own closing of a dialog
/// is, so that the character is free to act.
/// </para>
/// </remarks>
public static class NpcTalk
{
    // The dialog's end and the new map come together; one that names none within this has moved nobody.
    private static readonly TimeSpan _moveWindow = TimeSpan.FromSeconds(1);

    /// <summary>Talks to an NPC of the character's map, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="npcId">Which NPC, by id in the client's tables.</param>
    /// <param name="replies">The replies, by id: the first is given to the first question, and so on; none, the dialog is opened and left.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How it ended.</returns>
    public static async Task<NpcTalkOutcome> TalkAsync(this BotContext context, int npcId, IReadOnlyList<int> replies, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(replies);

        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is null || state.IsBusy)
            return NpcTalkOutcome.Busy;

        // The server places the NPCs of a map with it: one it did not place cannot be talked to.
        if (state.Npcs.Values.FirstOrDefault(npc => npc.NpcId == npcId) is not { } found)
            return NpcTalkOutcome.Missing;

        var (current, placed) = MapChange.ExpectNewMap(context, mapId);
        var asked = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is NpcDialogQuestionEvent or NpcActionErrorEvent or MapCurrentEvent);

        await context.SendAsync(new NpcActionRequest { Action = Banker.TalkAction, MapId = mapId, NpcActorId = found.ActorId }, cancellationToken).ConfigureAwait(false);

        switch (await asked.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false))
        {
            case null:
                return NpcTalkOutcome.NoAnswer;
            case NpcActionErrorEvent:
                return NpcTalkOutcome.Refused;
            case MapCurrentEvent:
                return Of(await MapChange.ArrivedAsync(context, current, placed, cancellationToken).ConfigureAwait(false));
        }

        for (var given = 0; ; given++)
        {
            if (context.State.Dialog is not { } dialog)
                return NpcTalkOutcome.NoAnswer;

            // The client shows the question before the reply or the leaving closes it.
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            if (given >= replies.Count)
            {
                await MapChange.LeaveDialogAsync(context, cancellationToken).ConfigureAwait(false);

                return NpcTalkOutcome.Done;
            }

            if (!dialog.Replies.Contains(replies[given]))
            {
                await MapChange.LeaveDialogAsync(context, cancellationToken).ConfigureAwait(false);

                return NpcTalkOutcome.Refused;
            }

            var answered = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is NpcDialogQuestionEvent or DialogLeaveEvent or MapCurrentEvent);

            await context.SendAsync(new NpcDialogReplyRequest { ReplyId = replies[given] }, cancellationToken).ConfigureAwait(false);

            switch (await answered.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false))
            {
                case null:
                    return NpcTalkOutcome.NoAnswer;
                case NpcDialogQuestionEvent:
                    continue;
                case MapCurrentEvent:
                    return Of(await MapChange.ArrivedAsync(context, current, placed, cancellationToken).ConfigureAwait(false));
            }

            // The dialog is over: the new map, when the reply names one, comes in the same millisecond.
            if (await current.WaitAsync(_moveWindow, cancellationToken).ConfigureAwait(false) is null)
                return NpcTalkOutcome.Done;

            return Of(await MapChange.ArrivedAsync(context, current, placed, cancellationToken).ConfigureAwait(false));
        }
    }

    private static NpcTalkOutcome Of(MapChangeOutcome outcome)
    {
        return outcome is MapChangeOutcome.Changed ? NpcTalkOutcome.Done : outcome is MapChangeOutcome.NotPlaced ? NpcTalkOutcome.NotPlaced : NpcTalkOutcome.NoAnswer;
    }
}
