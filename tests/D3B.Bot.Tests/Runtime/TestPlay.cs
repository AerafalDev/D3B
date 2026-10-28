using D3B.Bot.Characters;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Network;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Data.World;
using D3B.Engine.Maps;
using D3B.Engine.Criteria;
using D3B.Engine.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Runtime;

/// <summary>
/// A host's bots as a test plays them: a pack holding three maps and the world between them, every cell walkable but those
/// a test blocks on the first, the bots observing the game connections a test drives, and a clock the test moves.
/// </summary>
/// <remarks>
/// The world: <see cref="NorthMapId"/> is north of <see cref="MapId"/>, by a scroll each way, and by a map action from
/// <see cref="NorthActionCell"/>; a map action from <see cref="ActionCell"/> leads to <see cref="EastMapId"/>, whose west
/// edge scrolls back. Every cell of a map's edge leaves it straight across.
/// </remarks>
internal sealed class TestPlay : IAsyncDisposable
{
    public const long MapId = 191_104_002;
    public const long NorthMapId = 191_104_000;
    public const long EastMapId = 191_105_026;
    public const int ActionCell = 409;
    public const int NorthActionCell = 200;
    public const int SubAreaId = 440;
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

    public GameWorld World { get; }

    public Bots Bots { get; }

    public TestPlay(params int[] blocked)
    {
        var path = _directory.Combine("data-3.pack");

        File.WriteAllBytes(path, Pack(blocked));
        _pack = GamePack.Open(path);
        Maps = new GameMaps(_pack);
        World = new GameWorld(_pack);
        _logs = LoggerFactory.Create(logging => logging.AddProvider(new FakeLoggerProvider(Log)).SetMinimumLevel(LogLevel.Debug));
        Bots = new Bots(Maps, World, Time, _logs);
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
            SubAreaId = SubAreaId,
            Actors =
            {
                new Actor { Id = -1, Disposition = new ActorDisposition { CellId = 101 } },
                new Actor { Id = CharacterId, Disposition = new ActorDisposition { CellId = cell } },
            },
        };
    }

    private static byte[] Pack(int[] blocked)
    {
        var cells = Enumerable.Range(0, MapData.CellCount).Select(cell => blocked.Contains(cell) ? Wall() : Walkable(cell)).ToArray();
        var open = Enumerable.Range(0, MapData.CellCount).Select(Walkable).ToArray();
        var info = new PackInfo { ClientVersion = TestInstall.Version, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };

        using var stream = new MemoryStream();

        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(MapData.EntryName(MapId), new MapData(MapId, NorthMapId, 0, 0, 0, cells, Array.Empty<MapInteractive>()));
            writer.Add(MapData.EntryName(NorthMapId), new MapData(NorthMapId, 0, MapId, 0, 0, open, Array.Empty<MapInteractive>()));
            writer.Add(MapData.EntryName(EastMapId), new MapData(EastMapId, 0, 0, MapId, 0, open, Array.Empty<MapInteractive>()));
            writer.Add(WorldGraph.EntryName, Graph());
            writer.Add(WorldAtlas.EntryName, Atlas());
            writer.Complete();
        }

        return stream.ToArray();
    }

    private static MapCell Walkable(int cell)
    {
        var leaves = Enumerable.Range(0, MapCell.DirectionCount / 2).Select(side => 2 * side).Where(direction => MapGeometry.Neighbour(cell, direction) < 0);

        return new MapCell(MapCellProperties.Walkable | MapCellProperties.LineOfSight, 0, 0, leaves.Sum(direction => 1 << direction), 0, 1 << 4, 0);
    }

    private static MapCell Wall()
    {
        return new MapCell(MapCellProperties.None, 0, 0, 0, 0, 0, 0);
    }

    private static WorldGraph Graph()
    {
        // Vertices in ascending order of map: north, the first, east.
        WorldVertex[] vertices = [new(NorthMapId, 1), new(MapId, 1), new(EastMapId, 1)];
        WorldTransition[] transitions =
        [
            Transition(WorldTransitionType.Scroll, 2, 539, MapId),
            Transition(WorldTransitionType.Scroll, 6, 6, NorthMapId),
            Transition(WorldTransitionType.MapAction, WorldTransition.NoDirection, NorthActionCell, NorthMapId),
            Transition(WorldTransitionType.MapAction, WorldTransition.NoDirection, ActionCell, EastMapId),
            Transition(WorldTransitionType.Scroll, 4, 252, MapId),
        ];
        WorldEdge[] edges = [new(0, 1, 0, 1), new(1, 0, 1, 2), new(1, 2, 3, 1), new(2, 1, 4, 1)];

        return new WorldGraph(vertices, edges, transitions, Array.Empty<string>());
    }

    private static WorldTransition Transition(WorldTransitionType type, int direction, int cell, long mapId)
    {
        return new WorldTransition(type, direction, cell, WorldTransition.None, mapId, WorldTransition.None, WorldTransition.None);
    }

    private static WorldAtlas Atlas()
    {
        MapPosition[] positions = [new(NorthMapId, 4, -19, SubAreaId, 1, 0, 0), new(MapId, 4, -18, SubAreaId, 1, 0, 0), new(EastMapId, 5, -18, SubAreaId, 1, 0, 0)];
        SubArea[] subAreas = [new(SubAreaId, 0, 1, 10, SubArea.NoZaap, true, 0, 0)];

        return new WorldAtlas(positions, subAreas, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>());
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

    /// <summary>A connection of the account that chose the character, on a map, standing on a cell: the bot playing it.</summary>
    public async Task<(TestConnection Connection, BotActor Bot)> EnterAsync(int cell, long mapId = MapId)
    {
        var connection = Connect(Account);

        connection.Server(Chosen());
        Assert.True(Bots.TryGet(Key, out var bot));
        connection.Server(new MapCurrentEvent { MapId = mapId });
        connection.Server(Information(cell, mapId));
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    /// <summary>The first step of the path from where the bot's character stands to a map.</summary>
    public WorldStep StepTo(BotActor bot, long mapId)
    {
        var pathfinder = new WorldPathfinder(World.Graph, World.Atlas);
        var state = bot.State;

        Assert.True(pathfinder.TryGetVertex(Maps.Get(state.MapId!.Value), state.Cell!.Value, out var vertex));
        Assert.True(pathfinder.TryFind(vertex, new HashSet<long> { mapId }, new WorldPathRules { Facts = new CriterionFacts { Subscribed = true } }, out var path));

        return path.Steps[0];
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
