using System.Buffers;
using D3B.Network.Relay;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>Decodes every frame of the connection phase through the installed build's translation, and logs what it is (D49).</summary>
/// <remarks>
/// A frame is logged by the readable fields its oneofs hold, such as <c>request.identification</c>, and its length: never
/// by what it holds, since the connection phase carries the account's login token and ticket. A frame not understood
/// whole is a warning, and the relay forwards it as it is.
/// </remarks>
internal sealed class ConnectionPhaseDecoder : IRelayObserver
{
    private const int MaxDepth = 3;

    private readonly ProtocolTranslation _translation;
    private readonly ILogger _log;

    public ConnectionPhaseDecoder(ProtocolTranslation translation, ILogger<ConnectionPhaseDecoder> log)
    {
        _translation = translation;
        _log = log;
    }

    /// <summary>Names a message by the fields its oneofs hold, down from the root: what it is, without what it says.</summary>
    internal static string Describe(IMessage message)
    {
        var path = new List<string>();
        var current = message;

        while (current is not null && path.Count < MaxDepth)
        {
            var field = current.Descriptor.Oneofs
                .Where(oneof => !oneof.IsSynthetic)
                .Select(oneof => oneof.Accessor.GetCaseFieldDescriptor(current))
                .FirstOrDefault(field => field is not null);

            if (field is null)
                break;

            path.Add(field.Name);
            current = field.FieldType is FieldType.Message ? (IMessage)field.Accessor.GetValue(current) : null;
        }

        return path.Count is 0 ? message.Descriptor.Name : string.Join('.', path);
    }

    public void OnFrame(string connectionId, RelayDirection direction, ReadOnlySequence<byte> frame)
    {
        var decoded = _translation.DecodeConnection(frame);
        var side = direction is RelayDirection.FromClient ? "client" : "server";

        if (!_log.IsEnabled(decoded.IsUnderstood ? LogLevel.Information : LogLevel.Warning))
            return;

        var message = decoded.Message is null ? "no message" : Describe(decoded.Message);

        if (decoded.IsUnderstood)
            NetworkLog.Decoded(_log, connectionId, side, message, frame.Length);
        else
            NetworkLog.NotUnderstood(_log, connectionId, side, message, frame.Length, decoded.Problem);
    }
}
