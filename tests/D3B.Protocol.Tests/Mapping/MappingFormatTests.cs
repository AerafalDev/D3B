using System.Security.Cryptography;
using D3B.Protocol.Mapping;

namespace D3B.Protocol.Tests.Mapping;

/// <summary>
/// Fails when what a mapping depends on changes while <see cref="MappingFile.FormatVersion"/> stays: a host would then
/// read the mappings of its own format with another layout, or for another readable protocol (D45).
/// </summary>
/// <remarks>
/// The contract is the SHA-256 of the digests of <c>mapping.proto</c> and of the readable protocol
/// (<see cref="ProtocolDigest"/>): renaming a field, a value or a oneof, or editing a comment, changes neither.
/// </remarks>
public sealed class MappingFormatTests
{
    // The contract of each format, oldest first. Once a mapping of a format is published, its line is never edited: a
    // changed contract is a new format, and a new line.
    private static readonly (int Format, string Contract)[] _formats =
    [
        (1, "7ff4eda625c85f7c873a13e1a399934d4e83273f4faa67f902b4cde344fa32cd"),
        (2, "3fbef5dd21d2e3ec2affad1b747c1577fee40660f35e65be4c618eb1ae7a25f2"),
    ];

    private static string Contract()
    {
        return Convert.ToHexStringLower(SHA256.HashData([.. ProtocolDigest.Of([MappingReflection.Descriptor]), .. ReadableProtocol.Digest]));
    }

    [Fact]
    public void TheLastRecordedFormatIsTheCurrentOne()
    {
        var format = _formats[^1].Format;

        Assert.True(
            format == MappingFile.FormatVersion,
            $"MappingFile.FormatVersion is {MappingFile.FormatVersion}, where the last recorded format is {format}: record the contract of format {MappingFile.FormatVersion}.");

        for (var i = 1; i < _formats.Length; i++)
            Assert.True(_formats[i].Format > _formats[i - 1].Format, "The recorded formats must be in ascending order.");
    }

    [Fact]
    public void TheContractChangesOnlyWithTheFormatVersion()
    {
        var (format, recorded) = _formats[^1];
        var current = Contract();

        Assert.True(
            current == recorded,
            $"The mapping's contract is now {current}, where format {format} recorded {recorded}: raise MappingFile.FormatVersion, then record the new contract under it.");
    }
}
