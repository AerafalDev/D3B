using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Runtime;

/// <summary>Tells the client's requests that act on the character, which only the player sends, from those the client sends by itself (D66).</summary>
/// <remarks>
/// <para>
/// An observer never sees the requests D3B sends (D62), so every request it sees is the client's. The client sends some
/// by itself after an action, its own or D3B's: the confirmation of a walk, the request for a new map's information, and
/// the two requests to leave once the server closed an exchange (D60). Those are not the player's.
/// </para>
/// <para>
/// The requests that start an action are the player's: the eight kinds D3B sends (D60), and the stop of a walk, which the
/// client sends when the player gives a new path during one (client.md). Leaving a dialog is not one yet, since the client
/// sends it by itself after an exchange: telling the player's from the client's needs the dialog's state, at step 4's bank.
/// </para>
/// </remarks>
public static class PlayerActions
{
    /// <summary>Tells whether a request the client sent acts on the character, so the player sent it.</summary>
    /// <param name="request">A request the client sent, readable.</param>
    /// <returns>Whether the request starts or stops an action.</returns>
    public static bool Starts(IMessage request)
    {
        return request is MapMovementRequest or MapMovementCancelRequest or MapChangeRequest or InteractiveUseRequest or NpcActionRequest
            or NpcDialogReplyRequest or ExchangeTransferFromInventoryRequest or ExchangeKamasRequest;
    }
}
