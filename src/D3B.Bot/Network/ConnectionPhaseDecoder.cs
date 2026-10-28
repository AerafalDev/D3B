using D3B.Network.Relay;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>Watches every connection phase the relay takes, through the installed build's translation (D49, 3f).</summary>
/// <remarks>
/// Each connection phase gets its own <see cref="ConnectionPhaseSession"/>, which logs its frames by what they are and
/// rewrites the server selection's answer, so that the client's game connection comes to D3B. The game connection is not
/// the connection phase's protocol, and is not read here.
/// </remarks>
internal sealed class ConnectionPhaseDecoder : IRelayInterceptor
{
    private const int MaxDepth = 3;

    private readonly ProtocolTranslation _translation;
    private readonly GameServerSelections _selections;
    private readonly ILogger _log;

    public ConnectionPhaseDecoder(ProtocolTranslation translation, GameServerSelections selections, ILogger<ConnectionPhaseDecoder> log)
    {
        _translation = translation;
        _selections = selections;
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

    public IConnectionInterceptor? Intercept(string connectionId, RelayRoute route)
    {
        return route is RelayRoute.Connection ? new ConnectionPhaseSession(connectionId, _translation, _selections, _log) : null;
    }
}
