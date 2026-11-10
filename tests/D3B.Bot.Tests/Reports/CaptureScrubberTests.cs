using System.Buffers;
using System.Text;
using D3B.Bot.Reports;
using D3B.Bot.Tests.Network;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Reports;

public sealed class CaptureScrubberTests
{
    private const string Ticket = "3f9a0c47be1d5528a60c9e4f71d3b8a2";
    private const string Opaque = "0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f9";
    private const string FirstName = "Aldebarane-du-Nord";
    private const string SecondName = "Zephyrine";
    private const string ShortName = "Bo";
    private const string Stranger = "Passante-Inconnue";

    private static readonly CaptureHeader _header = new()
    {
        StartedAt = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero),
        ClientVersion = "3.7.13.17",
        Listener = "127.0.13.59:5556",
        ServerHost = "game.test",
        Server = "192.0.2.1:5555",
    };

    private readonly ProtocolTranslation _translation = TestGame.Translation();

    private static byte[] Framed(byte[] frame)
    {
        var writer = new ArrayBufferWriter<byte>();

        FrameWriter.Write(writer, frame);

        return writer.WrittenSpan.ToArray();
    }

    private static Character Character(long id, string name)
    {
        return new Character { Id = id, Basics = new CharacterBasics { Name = name, Level = 100 } };
    }

    private static byte[] UnknownRequest(string text)
    {
        var any = new Any { TypeUrl = "type.ankama.com/zzz", Value = new StringValue { Value = text }.ToByteString() };

        return new GameMessage { Request = new GameRequest { Id = -1, Content = any } }.ToByteArray();
    }

    private static byte[] UnknownEvent(string text)
    {
        var any = new Any { TypeUrl = "type.ankama.com/yyy", Value = new StringValue { Value = text }.ToByteString() };

        return new GameMessage { Event = new GameEvent { Content = any } }.ToByteArray();
    }

    private static Actor Named(long id, string name)
    {
        return new Actor { Id = id, Information = new ActorInformation { Roleplay = new RoleplayActor { Named = new NamedActor { Name = name } } } };
    }

    /// <summary>A short session: the ticket, the list, an opaque string, the choice, the map with the character and a stranger, and a name in a longer text.</summary>
    private static List<(CaptureRecordKind Kind, byte[] Data)> Session()
    {
        return
        [
            (CaptureRecordKind.FromClient, Framed(TestGame.Request(new AuthenticationRequest { Ticket = Ticket, Language = "fr" }))),
            (CaptureRecordKind.FromServer, Framed(TestGame.Event(new AuthenticationAcceptedEvent()))),
            (CaptureRecordKind.FromClient, Framed(TestGame.Request(new CharacterListRequest()))),
            (CaptureRecordKind.FromClient, Framed(UnknownRequest(Opaque))),
            (CaptureRecordKind.FromServer, Framed(TestGame.Event(new CharacterListEvent { Characters = { Character(11, FirstName), Character(12, SecondName), Character(13, ShortName) } }))),
            (CaptureRecordKind.FromClient, Framed(TestGame.Request(new CharacterSelectionRequest { CharacterId = 11 }))),
            (CaptureRecordKind.FromServer, Framed(TestGame.Event(new CharacterSelectionEvent { Success = new CharacterSelectionEvent.Types.Success { Character = Character(11, FirstName) } }))),
            (CaptureRecordKind.FromServer, Framed(TestGame.Event(new MapInformationEvent { MapId = 188744706, Actors = { Named(11, FirstName), Named(30, Stranger), Named(31, ShortName) } }))),
            (CaptureRecordKind.FromServer, Framed(UnknownEvent("Welcome " + SecondName + " and " + ShortName))),
            (CaptureRecordKind.ServerClosed, []),
        ];
    }

    private static byte[] Capture(List<(CaptureRecordKind Kind, byte[] Data)> records, int cutEvery = 0)
    {
        var time = new FakeTimeProvider();

        using var stream = new MemoryStream();

        using (var writer = new CaptureWriter(stream, _header, time))
        {
            foreach (var (kind, data) in records)
            {
                time.Advance(TimeSpan.FromMilliseconds(3));

                if (data.Length == 0)
                {
                    writer.WriteClosed(kind);

                    continue;
                }

                // A read cuts a frame where the network cut it.
                var step = cutEvery > 0 ? cutEvery : data.Length;

                for (var start = 0; start < data.Length; start += step)
                    writer.Write(kind, new ReadOnlySequence<byte>(data.AsMemory(start, Math.Min(step, data.Length - start))));
            }
        }

        return stream.ToArray();
    }

    private (byte[] Output, CaptureScrub Result) Scrub(byte[] capture)
    {
        var output = new MemoryStream();

        // The scrubber closes its output, and a memory stream still gives its bytes once closed.
        var result = new CaptureScrubber(_translation).Scrub(new MemoryStream(capture), output);

        return (output.ToArray(), result);
    }

    private static bool Holds(byte[] bytes, string text)
    {
        return bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0;
    }

    private static List<CaptureRecord> Records(byte[] capture)
    {
        using var reader = new CaptureReader(new MemoryStream(capture));

        return [.. reader.ReadAll()];
    }

    [Fact]
    public void NoOutputHoldsTheTicketTheOpaqueStringOrTheNamesOfTheAccountsCharacters()
    {
        var capture = Capture(Session());

        Assert.True(Holds(capture, Ticket));
        Assert.True(Holds(capture, FirstName));

        var (output, _) = Scrub(capture);

        Assert.False(Holds(output, Ticket));
        Assert.False(Holds(output, Opaque));
        Assert.False(Holds(output, Opaque[..32]));
        Assert.False(Holds(output, FirstName));
        Assert.False(Holds(output, SecondName));
    }

    [Fact]
    public void NoOutputHoldsTheTicketOrANameWhateverWayTheNetworkCutTheReads()
    {
        foreach (var cut in new[] { 1, 2, 3, 5, 7, 16, 31, 50 })
        {
            var (output, _) = Scrub(Capture(Session(), cut));

            Assert.False(Holds(output, Ticket), $"cut every {cut} bytes");
            Assert.False(Holds(output, FirstName), $"cut every {cut} bytes");
            Assert.False(Holds(output, SecondName), $"cut every {cut} bytes");
        }
    }

    [Fact]
    public void TheCopyHasTheSameHeaderRecordsKindsOffsetsAndLengths()
    {
        var capture = Capture(Session(), 9);
        var (output, _) = Scrub(capture);
        var before = Records(capture);
        var after = Records(output);

        using var reader = new CaptureReader(new MemoryStream(output));

        Assert.Equal(_header, reader.Header);
        Assert.Equal(capture.Length, output.Length);
        Assert.Equal(before.Select(record => (record.Kind, record.Offset, record.Data.Length)), after.Select(record => (record.Kind, record.Offset, record.Data.Length)));
    }

    [Fact]
    public void ACopyIsReadAsTheOriginalWasWithTheSameMessagesAndNothingOfTheAccount()
    {
        var (output, _) = Scrub(Capture(Session()));
        var messages = new List<IMessage>();

        foreach (var record in Records(output).Where(record => record.Kind is CaptureRecordKind.FromClient or CaptureRecordKind.FromServer))
        {
            var sequence = new SequenceReader<byte>(new ReadOnlySequence<byte>(record.Data));

            while (FrameReader.TryReadFrame(ref sequence, out var frame))
            {
                if (_translation.DecodeGame(frame) is { IsRead: true, Content: { } content })
                    messages.Add(content);
            }
        }

        var request = Assert.IsType<AuthenticationRequest>(messages[0]);
        var list = Assert.Single(messages.OfType<CharacterListEvent>());

        Assert.Equal(Ticket.Length, request.Ticket.Length);
        Assert.NotEqual(Ticket, request.Ticket);
        Assert.Equal("fr", request.Language);
        Assert.Equal([FirstName.Length, SecondName.Length, ShortName.Length], list.Characters.Select(character => character.Basics.Name.Length));
        Assert.Equal(3, list.Characters.Select(character => character.Basics.Name).Distinct().Count());
        Assert.Equal([11L, 12L, 13L], list.Characters.Select(character => character.Id));
        Assert.Equal(list.Characters[0].Basics.Name, Assert.Single(messages.OfType<CharacterSelectionEvent>()).Success.Character.Basics.Name);
        Assert.Equal(list.Characters[0].Basics.Name, Assert.Single(messages.OfType<MapInformationEvent>()).Actors[0].Information.Roleplay.Named.Name);

        // Even the name of two letters is gone from every field that held it, and the stranger's stays.
        Assert.DoesNotContain(list.Characters, character => character.Basics.Name == ShortName);
        Assert.DoesNotContain(messages.OfType<MapInformationEvent>().Single().Actors, actor => actor.Information.Roleplay.Named.Name is ShortName or FirstName);
        Assert.Contains(messages.OfType<MapInformationEvent>().Single().Actors, actor => actor.Information.Roleplay.Named.Name == Stranger);
    }

    [Fact]
    public void ANameWithAccentsIsOverwrittenWhateverItsBytesAndKeepsItsByteLength()
    {
        const string Accented = "Éléonore-du-Nord";

        var records = Session();

        records[4] = (CaptureRecordKind.FromServer, Framed(TestGame.Event(new CharacterListEvent { Characters = { Character(11, Accented) } })));
        records[6] = (CaptureRecordKind.FromServer, Framed(TestGame.Event(new CharacterSelectionEvent { Success = new CharacterSelectionEvent.Types.Success { Character = Character(11, Accented) } })));
        records[7] = (CaptureRecordKind.FromServer, Framed(TestGame.Event(new MapInformationEvent { Actors = { Named(11, Accented) } })));

        var capture = Capture(records);
        var (output, _) = Scrub(capture);

        Assert.True(Holds(capture, Accented));
        Assert.False(Holds(output, Accented));
        Assert.Equal(capture.Length, output.Length);
    }

    [Fact]
    public void ASecretInAFrameD3BSentTheServerIsOverwrittenToo()
    {
        const string Injected = "fedcba9876543210fedcba9876543210fedcba98";

        var records = Session();

        records.Insert(9, (CaptureRecordKind.Injected, Framed(UnknownRequest(Injected))));

        var (output, _) = Scrub(Capture(records));

        Assert.False(Holds(output, Injected));
    }

    [Fact]
    public void BytesThatAreNoFramesAreCopiedWithoutFailingAndTheSecretsAroundThemStillGo()
    {
        var records = Session();

        // A length of six bytes of continuation, then one claiming more than the capture holds.
        records.Insert(9, (CaptureRecordKind.FromClient, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01]));
        records.Insert(9, (CaptureRecordKind.FromClient, [0xE8, 0x07, 0x0A, 0x20]));

        var (output, _) = Scrub(Capture(records));

        Assert.False(Holds(output, Ticket));
        Assert.False(Holds(output, Opaque));
    }

    [Fact]
    public void AFrameCutShortByTheEndOfTheCaptureLosesItsSecretToo()
    {
        var records = Session();
        var frame = Framed(UnknownRequest("1122334455667788990011223344556677889900aabbccddeeff"));

        // The frame is whole in the client's bytes but the capture ends inside it.
        records.Insert(9, (CaptureRecordKind.FromClient, frame[..40]));

        var (output, _) = Scrub(Capture(records));

        Assert.False(Holds(output, "1122334455667788990011223344556677889900"));
    }

    [Fact]
    public void ANameIsFoundByValueWhereverAFieldHoldsIt()
    {
        var (output, result) = Scrub(Capture(Session()));
        var map = Records(output).Where(record => record.Kind is CaptureRecordKind.FromServer).SelectMany(record => record.Data.ToArray()).ToArray();

        Assert.Equal(3, result.Names);
        Assert.False(Holds(map, FirstName));
        Assert.False(Holds(map, SecondName));

        // The stranger is the server's data and stays.
        Assert.True(Holds(map, Stranger));
    }

    [Fact]
    public void ANameOfLessThanSixBytesIsOverwrittenOnlyAsAWholeFieldNotInsideAText()
    {
        var (output, _) = Scrub(Capture(Session()));
        var text = Records(output).Where(record => record.Kind is CaptureRecordKind.FromServer).SelectMany(record => record.Data.ToArray()).ToArray();

        Assert.False(Holds(text, "Welcome " + SecondName));
        Assert.True(Holds(text, " and " + ShortName));
    }

    [Fact]
    public void AValueOfThirtyTwoHexCharactersOrMoreFromTheClientIsASecretAndOneOfThirtyOneIsNot()
    {
        var records = Session();

        records.Insert(3, (CaptureRecordKind.FromClient, Framed(UnknownRequest("0123456789abcdef0123456789abcde"))));
        records.Insert(3, (CaptureRecordKind.FromClient, Framed(UnknownRequest("0123456789ABCDEF0123456789ABCDEF"))));

        var (output, result) = Scrub(Capture(records));

        Assert.True(Holds(output, "0123456789abcdef0123456789abcde"));
        Assert.True(Holds(output, "0123456789ABCDEF0123456789ABCDEF"));
        Assert.Equal(2, result.Secrets);
    }

    [Fact]
    public void ASecretIsOverwrittenWhereverElseItsValueIsFoundOnEitherSide()
    {
        var records = Session();

        records.Add((CaptureRecordKind.FromServer, Framed(UnknownEvent("echo " + Ticket))));

        var (output, _) = Scrub(Capture(records));

        Assert.False(Holds(output, Ticket));
    }

    [Fact]
    public void ACaptureWithoutACharacterListIsNotCleanedForWantOfWhatToHide()
    {
        var records = Session();

        // The list and the choice are the fifth and the seventh records.
        records.RemoveAt(6);
        records.RemoveAt(4);

        var exception = Assert.Throws<CaptureScrubException>(() => Scrub(Capture(records)));

        Assert.Contains("character list", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrubbingACopyAgainChangesNothing()
    {
        var (once, _) = Scrub(Capture(Session()));
        var (twice, result) = Scrub(once);

        Assert.Equal(once, twice);
        Assert.Equal(0, result.Fields);
    }

    [Fact]
    public void ACaptureCutShortMidFrameIsCleanedUpToItsEnd()
    {
        var capture = Capture(Session());
        var cut = capture[..^25];
        var (output, _) = Scrub(cut);

        Assert.False(Holds(output, Ticket));
        Assert.False(Holds(output, FirstName));
        Assert.True(output.Length <= cut.Length);
    }

    [Fact]
    public void TheResultSaysHowManyNamesSecretsAndFieldsWereOverwritten()
    {
        var (_, result) = Scrub(Capture(Session()));

        // The ticket, the opaque string; the names in the list (3), the selection (1), the actors (2), the text (1 of 2).
        Assert.Equal(3, result.Names);
        Assert.Equal(2, result.Secrets);
        Assert.Equal(9, result.Fields);
    }
}
