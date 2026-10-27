using D3B.Protocol.Connection;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>D3B's readable protocol: the <c>.proto</c> files bot code reads messages through, which a mapping pairs with a build's.</summary>
public static class ReadableProtocol
{
    /// <summary>The package of the connection phase's files.</summary>
    public const string ConnectionPackage = "d3b.connection";

    /// <summary>The file of a build's schema that holds the connection phase (D39).</summary>
    public const string ConnectionSchemaFile = "connection.proto";

    /// <summary>Gets every readable file.</summary>
    public static IReadOnlyList<FileDescriptor> Files { get; } =
    [
        ConnectionReflection.Descriptor,
        IdentificationReflection.Descriptor,
        ServersReflection.Descriptor,
        AccountsReflection.Descriptor,
        FriendsReflection.Descriptor,
    ];

    /// <summary>Gets, for each readable package, the file of a build's schema its messages are looked for in.</summary>
    /// <remarks>The client keeps the connection phase and the game in two assemblies, so a type is never in the other.</remarks>
    public static IReadOnlyDictionary<string, string> Scopes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ConnectionPackage] = ConnectionSchemaFile,
    };

    /// <summary>Gets the anchors the captures of a build are read with.</summary>
    /// <remarks>
    /// The client's frames are requests and the server's responses; a connection's first request identifies the account
    /// and its second chooses a server, as every connection captured did (docs/client.md).
    /// </remarks>
    public static AnchorPlan ConnectionAnchors { get; } = new(
        ConnectionMessage.Descriptor,
        ConnectionMessage.Descriptor.FindFieldByNumber(ConnectionMessage.RequestFieldNumber),
        ConnectionMessage.Descriptor.FindFieldByNumber(ConnectionMessage.ResponseFieldNumber),
        [
            new AnchorExchange(
                ConnectionRequest.Descriptor.FindFieldByNumber(ConnectionRequest.IdentificationFieldNumber),
                ConnectionResponse.Descriptor.FindFieldByNumber(ConnectionResponse.IdentificationFieldNumber)),
            new AnchorExchange(
                ConnectionRequest.Descriptor.FindFieldByNumber(ConnectionRequest.ServerSelectionFieldNumber),
                ConnectionResponse.Descriptor.FindFieldByNumber(ConnectionResponse.ServerSelectionFieldNumber)),
        ]);

    /// <summary>Gets the digest of the readable protocol's contract, which a mapping of it records.</summary>
    public static IReadOnlyList<byte> Digest { get; } = ProtocolDigest.Of(Files);
}
