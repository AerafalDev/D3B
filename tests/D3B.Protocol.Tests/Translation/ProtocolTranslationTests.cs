using System.Buffers;
using D3B.Network.Tests;
using D3B.Protocol.Connection;
using D3B.Protocol.Mapping;
using D3B.Protocol.Tests.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;

namespace D3B.Protocol.Tests.Translation;

public sealed class ProtocolTranslationTests
{
    private const string Version = "3.7.12.12";

    /// <summary>Maps the readable protocol against its obfuscation, as a test changed the schema and the mapping, then translates it.</summary>
    private static ProtocolTranslation Translate(Action<Obfuscation>? change = null, Action<Obfuscation, ProtocolMapping>? edit = null)
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var mapping = new ProtocolMatcher(ReadableProtocol.Files, obfuscation.Schema(), ReadableProtocol.Scopes).Match(null, [])
            .ToMapping(Version, obfuscation.Schema().Sha256, ReadableProtocol.Digest, "test");

        change?.Invoke(obfuscation);
        edit?.Invoke(obfuscation, mapping);

        var schema = obfuscation.Schema();

        mapping.SchemaSha256 = ByteString.CopyFrom([.. schema.Sha256]);

        return ProtocolTranslation.Create(schema, mapping);
    }

    private static ConnectionMessage ServerSelection()
    {
        return new ConnectionMessage
        {
            Response = new ConnectionResponse
            {
                Id = "b",
                ServerSelection = new ServerSelectionResponse
                {
                    Success = new ServerSelectionResponse.Types.Success { Ticket = new string('t', 32), Host = "game.test", Ports = { 5555, 443 } },
                },
            },
        };
    }

    [Fact]
    public void AMappingThatHoldsIsTranslated()
    {
        var translation = Translate();

        Assert.Equal(Version, translation.ClientVersion);
        Assert.True(translation.Audit.Passed);
        Assert.Equal(37, translation.Audit.Messages);
    }

    [Fact]
    public void AMappingThatFailsTheAuditIsRefused()
    {
        var exception = Assert.Throws<ProtocolMismatchException>(() => Translate(edit: (_, mapping) => mapping.Messages.Remove(mapping.Messages.Single(pair => pair.Readable == Ping.Descriptor.FullName))));

        Assert.Contains("d3b.connection.Ping has no counterpart.", exception.Failures);
        Assert.StartsWith($"The mapping of client {Version} does not hold: {exception.Failures.Count} failures, the first: ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ABuildRenumberingAFieldIsRefused()
    {
        // The audit holds: the counterpart's field 7 has the shape of the readable field 1. Only its number differs.
        var exception = Assert.Throws<ProtocolMismatchException>(() => Translate(
            obfuscation => obfuscation.Message(ServerSelectionRequest.Descriptor.FullName).Field.Single(field => field.Number == 1).Number = 7,
            (_, mapping) => mapping.Messages.Single(pair => pair.Readable == ServerSelectionRequest.Descriptor.FullName).Fields.Single().Obfuscated = 7));

        var failure = Assert.Single(exception.Failures);

        Assert.StartsWith("d3b.connection.ServerSelectionRequest field 1 is field 7 of .", failure, StringComparison.Ordinal);
        Assert.EndsWith("the build renumbers it.", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ABuildRenumberingAnEnumValueIsRefused()
    {
        const string accessibility = "d3b.connection.ServerInformation.Accessibility";

        var exception = Assert.Throws<ProtocolMismatchException>(() => Translate(
            obfuscation => obfuscation.Enum(accessibility).Value.Single(value => value.Number == 3).Number = 9,
            (_, mapping) => mapping.Enums.Single(pair => pair.Readable == accessibility).Values.Single(value => value.Readable == 3).Obfuscated = 9));

        var failure = Assert.Single(exception.Failures);

        Assert.StartsWith($"{accessibility} value 3 is value 9 of .", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AFrameOfTheConnectionIsUnderstoodWholeWhereverTheNetworkCutIt()
    {
        var bytes = ServerSelection().ToByteArray();
        var decoded = Translate().DecodeConnection(TestSequences.Segments(bytes, 7));

        Assert.True(decoded.IsUnderstood, decoded.Problem);
        Assert.Equal(ServerSelection(), decoded.Message);
    }

    [Fact]
    public void AFrameHoldingWhatTheReadableProtocolLacksIsNotUnderstood()
    {
        // Field 15 of the root, a varint: no readable message has it.
        byte[] bytes = [.. ServerSelection().ToByteArray(), 15 << 3, 1];
        var decoded = Translate().DecodeConnection(new ReadOnlySequence<byte>(bytes));

        Assert.False(decoded.IsUnderstood);
        Assert.Equal(ServerSelection(), decoded.Message);
        Assert.Contains($"encodes back to {bytes.Length - 2} of its {bytes.Length} bytes", decoded.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void BytesThatAreNoMessageHaveNone()
    {
        // A length-delimited field 2 declaring more bytes than the frame holds.
        var decoded = Translate().DecodeConnection(new ReadOnlySequence<byte>([0x12, 0x7F, 0x01]));

        Assert.False(decoded.IsUnderstood);
        Assert.Null(decoded.Message);
        Assert.StartsWith("it is no connection message: ", decoded.Problem, StringComparison.Ordinal);
    }
}
