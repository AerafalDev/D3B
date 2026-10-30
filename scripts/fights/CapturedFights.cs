using System.Buffers;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf;

namespace D3B.Scripts.Fights;

/// <summary>The messages the server sent a game connection, read through the translation, in the order they came.</summary>
internal static class CapturedFights
{
    public static List<IMessage> ServerMessages(ProtocolTranslation translation, string path)
    {
        var messages = new List<IMessage>();
        var pending = new List<byte>();

        using var reader = CaptureReader.Open(path);

        foreach (var record in reader.ReadAll())
        {
            if (record.Kind is not CaptureRecordKind.FromServer)
                continue;

            pending.AddRange(record.Data.Span);
            var sequence = new SequenceReader<byte>(new ReadOnlySequence<byte>(pending.ToArray()));

            while (FrameReader.TryReadFrame(ref sequence, out var frame))
            {
                if (translation.DecodeGame(frame) is { IsRead: true, Content: { } content })
                    messages.Add(content);
            }

            pending.RemoveRange(0, (int)sequence.Consumed);
        }

        return messages;
    }

    /// <summary>Gets, for each fight's start in the messages, the character the server lets play: the client's own fighter.</summary>
    public static Dictionary<int, long> Characters(List<IMessage> messages)
    {
        var characters = new Dictionary<int, long>();

        for (var index = 0; index < messages.Count; index++)
        {
            if (messages[index] is not FightStartingEvent)
                continue;

            long? playing = null;

            for (var next = index + 1; next < messages.Count && messages[next] is not FightStartingEvent; next++)
            {
                if (messages[next] is FightTurnEvent turn)
                    playing = turn.FighterId;

                if (messages[next] is FightTurnStartPlayingEvent && playing is { } character)
                {
                    characters[index] = character;

                    break;
                }
            }
        }

        return characters;
    }
}
