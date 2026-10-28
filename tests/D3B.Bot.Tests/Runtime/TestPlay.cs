using D3B.Bot.Characters;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Network;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Runtime;

/// <summary>
/// A host's bots as a test plays them: a pack holding one map, every cell walkable but those a test blocks, the bots
/// observing the game connections a test drives, and a clock the test moves.
/// </summary>
internal sealed class TestPlay : IAsyncDisposable
{
    public const long MapId = 191_104_002;
    public const long AccountId = 7;
    public const long CharacterId = 70_001;

    private static readonly RelaySettings _settings = new()
    {
        Address = RelaySettings.DefaultAddress,
        Ports = RelaySettings.DefaultPorts,
        GamePorts = RelaySettings.DefaultGamePorts,
        ClientDirectory = "no-client",
        RedirectDirectory = "no-redirect",
    };

    private readonly TemporaryDirectory _directory = new();
    private readonly GamePack _pack;
    private readonly ILoggerFactory _logs;
    private readonly GameAccounts _accounts;
    private readonly RelayDecoder _decoder;
    private readonly List<TestConnection> _connections = [];

    public static GameAccount Account { get; } = new(AccountId, "nickname", "tag");

    public static CharacterKey Key { get; } = new(AccountId, CharacterId);

    public FakeTimeProvider Time { get; } = new();

    public FakeLogCollector Log { get; } = new();

    public ProtocolTranslation Translation { get; } = TestGame.Translation();

    public GameMaps Maps { get; }

    public Bots Bots { get; }

    public TestPlay(params int[] blocked)
    {
        var path = _directory.Combine("data-3.pack");

        File.WriteAllBytes(path, Pack(blocked));
        _pack = GamePack.Open(path);
        Maps = new GameMaps(_pack);
        _logs = LoggerFactory.Create(logging => logging.AddProvider(new FakeLoggerProvider(Log)).SetMinimumLevel(LogLevel.Debug));
        Bots = new Bots(Maps, Time, _logs);
        _accounts = new GameAccounts(_settings.SelectionLifetime, Time);
        _decoder = new RelayDecoder(Translation, new GameServerSelections(_settings, Time), _accounts, [Bots], _logs);
    }

    /// <summary>The character as the server's confirmation of the choice describes it.</summary>
    public static CharacterSelectionEvent Chosen(long characterId = CharacterId)
    {
        return new CharacterSelectionEvent
        {
            Success = new CharacterSelectionEvent.Types.Success
            {
                Character = new Character { Id = characterId, Basics = new CharacterBasics { Name = "Character", Level = 200 } },
            },
        };
    }

    /// <summary>What is on the map, the character on a cell.</summary>
    public static MapInformationEvent Information(int cell, long mapId = MapId)
    {
        return new MapInformationEvent
        {
            MapId = mapId,
            SubAreaId = 440,
            Actors =
            {
                new Actor { Id = -1, Disposition = new ActorDisposition { CellId = 101 } },
                new Actor { Id = CharacterId, Disposition = new ActorDisposition { CellId = cell } },
            },
        };
    }

    private static byte[] Pack(int[] blocked)
    {
        var walkable = new MapCell(MapCellProperties.Walkable | MapCellProperties.LineOfSight, 0, 0, 0, 0, 1 << 4, 0);
        var wall = new MapCell(MapCellProperties.None, 0, 0, 0, 0, 0, 0);
        var cells = Enumerable.Range(0, MapData.CellCount).Select(cell => blocked.Contains(cell) ? wall : walkable).ToArray();
        var info = new PackInfo { ClientVersion = TestInstall.Version, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };

        using var stream = new MemoryStream();

        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(MapData.EntryName(MapId), new MapData(MapId, 0, 0, 0, 0, cells, Array.Empty<MapInteractive>()));
            writer.Complete();
        }

        return stream.ToArray();
    }

    /// <summary>A game connection, its first request presenting a ticket the connection phase gave an account, or none.</summary>
    public TestConnection Connect(GameAccount? account)
    {
        var id = $"g{_connections.Count + 1}";
        var ticket = $"ticket-of-{id}";
        var fake = new FakeRelayConnection(id, RelayRoute.Game);
        var connection = new TestConnection(fake, _decoder.Intercept(fake)!, Translation);

        if (account is not null)
            _accounts.Remember(ticket, account);

        _connections.Add(connection);
        connection.Client(new AuthenticationRequest { Ticket = ticket, Language = "fr" });

        return connection;
    }

    /// <summary>A connection of the account that chose the character, on the map, standing on a cell: the bot playing it.</summary>
    public async Task<(TestConnection Connection, BotActor Bot)> EnterAsync(int cell)
    {
        var connection = Connect(Account);

        connection.Server(Chosen());
        Assert.True(Bots.TryGet(Key, out var bot));
        connection.Server(new MapCurrentEvent { MapId = MapId });
        connection.Server(Information(cell));
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    public async ValueTask DisposeAsync()
    {
        await Bots.DisposeAsync();

        foreach (var connection in _connections)
            connection.Dispose();

        _logs.Dispose();
        _pack.Dispose();
        _directory.Dispose();
    }
}
