# Architecture

Where the bot is going. The reasons behind each choice are in [decisions.md](decisions.md), and the order of work is
in [roadmap.md](roadmap.md). Built so far: the solution, the module and plugin model in `D3B.Core`, and a host that
builds its modules, starts and stops (step 1); the data pack format in `D3B.Data`, the tools that inspect the client,
extract its maps, world graph and game tables with the texts they name, and publish a build's pack, and the data module,
whose pack the host finds or downloads before it starts (step 2); the network module, whose relay takes each client's
connection phase through D3B, with its framing measured and its captures (step 3, session 3b); the tool writing each
build's obfuscated protocol schema, read from the client's code (step 3, session 3c); the readable protocol of the
connection phase, and the tool matching it with a build's schema into the build's mapping, audited (step 3, session
3d). Everything else below is the target.

## Overview

```text
Official client (Unity)                 scripts/ (file-based apps, D10)              release artifacts, per client build
Dofus_Data/StreamingAssets/** ────────► extraction: Unity readers ───────────────►  data pack (MemoryPack + zstd)
GameAssembly.dll + metadata   ────────► mapping: schema, captures, matching ─────►  schema, protocol mapping
                                                                                             │ fetched and loaded by
                                                                                             ▼
D3B.App (Avalonia, ShadUI) ◄── gRPC on a named pipe (D7) ──► D3B.Host (headless: Generic Host, Kestrel)
                                                                  │ BotBuilder: default modules + plugins/ (D5, D6)
                                                                  ▼
                                                             one async actor per bot (D9)
                                                                  │ acts through
                                                                  ▼
official client, one per account ──── its configuration, ──► D3B.Network: Kestrel   ────►  Ankama's connection
started by the official launcher      then a rewritten       on a loopback address         server, then game server
                                      address (D34, D35)     (D8, D23, D36)
```

The host refuses to start when the protocol mapping it loaded does not match the installed client's build (D11).

## Projects

Confirmed at step 1 (D19). Every project exists in `D3B.slnx`; those with no code yet are empty class libraries, and
each step adds the references it needs.

| Path | Role |
| --- | --- |
| `src/D3B.Core` | Modules and `BotBuilder`, plugin loading, shared abstractions. What a plugin references. |
| `src/D3B.Network` | Kestrel transport, framing, codec over `IDuplexPipe`. The only project referencing `Microsoft.AspNetCore.App`, with the host. |
| `src/D3B.Protocol` | Classes generated from the readable `.proto` files, a build's mapping, its matching and audit, and the per-build translation. |
| `src/D3B.Data` | Data pack format and runtime readers: maps, cells, world graph, items, spells, monsters, interactives, texts. |
| `src/D3B.Engine` | Pure logic with no I/O: cell and world pathfinding, line of sight, fight simulation. |
| `src/D3B.Bot` | The bot's run time: actor, state, behaviors, recovery, and the default modules. |
| `src/D3B.Scripting` | The Lua host and its API namespaces (D12). |
| `src/D3B.Host` | The headless host: Generic Host, gRPC server on a named pipe, storage, logs. |
| `src/D3B.Contracts` | The gRPC contracts shared by the host and the app. |
| `src/D3B.App` | The Avalonia desktop app. |
| `scripts/` | Inspection, extraction, release and mapping tools, as file-based apps. |
| `templates/` | The `dotnet new d3b-plugin` template, compiling against the `D3B.Core` package (D22). |
| `samples/` | Example plugins, built with the solution and loaded by the tests. |
| `tests/`, `benchmarks/` | One test project per `src/` project that has logic; benchmarks for the codec and pathfinding. |

## Modules and plugins

Built at step 1, in `D3B.Core` (D20, D21):

- A module implements `IModule.Build(BotBuilder)`. It registers services, message handlers, activities, fight
  strategies, Lua API namespaces, settings and UI pages. `BotBuilder` wraps the Generic Host's builder and is an
  `IHostApplicationBuilder`: a module registers through `Services`, `Configuration` and `Logging`, and `Build()` returns
  the `IHost`.
- A module is built as soon as it is added. It requires another by calling `bot.AddModule<T>()` first in its `Build`.
  Adding a module type twice builds it once.
- A group (`IModuleGroup`) lists modules in order; whoever adds it may disable some. `DefaultModules`, in `D3B.Bot`,
  gathers the standard modules: so far the data module (D31), then, as the steps write them, network, protocol, world,
  movement, harvesting, bank. D3B's own features are modules like any other, so they can be disabled, and later replaced
  (D20).
- A plugin is a folder under `plugins/` holding an assembly named after it, whose one public class marked
  `[Plugin("id")]` is its root module. It loads once into its own `AssemblyLoadContext`, which asks the host's context
  first and is not collectible (D6). An invalid plugin stops the host from starting.
- `D3B.Host` adds `DefaultModules`, then the plugins next to its executable (or under `Plugins:Directory`), logs what
  it built, gets the pack of the installed client's build ready (Game data, below), and runs until it is stopped.
- A plugin compiles against the `D3B.Core` package; `dotnet new d3b-plugin` creates one (D22).
- A plugin's UI part, loaded by the app, is Q6.

## Bot run time

- Each bot is an actor: one `Channel<T>` inbox, one logical thread, no locks on its state (D9).
- The inbox receives decoded messages, timer expiries and commands from the app. Handlers update the state. Logic code
  reads immutable snapshots of it.
- Behaviors are async methods taking a `CancellationToken`. An interruption (a fight, a disconnection, full pods)
  cancels the current behavior. A recovery step runs, then the behavior resumes or the activity picks the next one.
- Every delay goes through `TimeProvider`.
- When the client reconnects, the activity resumes where its state allows.

## Network and protocol

Built in session 3b (D35 to D38): the relay of the connection phase, in `D3B.Network`, registered by the network module
(`NetworkModule`, in `D3B.Bot`, after the data module).

- MITM only (D23): Kestrel takes each official client's connection, and D3B opens the matching connection to the server.
  Both sides are a `ConnectionContext` (D8). `RelayServer` runs Kestrel's server as a hosted service, with no web host,
  on `Network:Address` (127.0.13.59) and `Network:Ports` (5555 and 443); a port it cannot take stops the host (D36).
- How a client reaches D3B (D35): the player starts it from the official launcher. While the host runs, `ClientRedirect`
  points the client's `ConfigUrl` at a copy of Ankama's configuration whose connection host is D3B's address, a `file:`
  URL, and gives the client Ankama's back when the host stops; it follows the launcher rewriting the file.
  `scripts/client.cs` checks it and restores it after a crash. No `hosts` line, no elevation.
- `RelayConnectionHandler` resolves the connection host Ankama's configuration names, connects on the port the client
  chose, and copies the bytes as they are, both ways, any number of clients at once. A side closing closes the other.
- For now only the connection phase comes through D3B. The connection server then sends the game server's address: D3B
  rewrites it to its game listener, and connects to the real game server when the client arrives there (D34, session
  3e).
- The client's other connections, to the launcher, Haapi, the chat and the CDN, do not go through D3B.
- The bot acts through that connection, and the client is kept consistent with what it does: Q9.
- Framing, measured (D38): a varint length, then a protobuf message, in the clear. `FrameReader` reads frames from a
  `SequenceReader<byte>` without copying; a benchmark in `benchmarks/D3B.Network.Benchmarks` measures it. The game
  connection's envelope carries an `Any` whose type URL is the obfuscated message name, prefixed with
  `type.ankama.com`, as the references read it; the connection phase has none.
- The obfuscated schema, built in session 3c (D39, D40): `scripts/schema.cs` reads the installed build's protocol from
  its IL2CPP code with Cpp2IL, its structure from the metadata and the encoding of every field from the machine code,
  and writes it as a `FileDescriptorSet` beside the build's pack, `schema-1.binpb`. `scripts/capture.cs --schema` reads
  captures with it, field by field.
- The readable protocol, built in session 3d for the connection phase (D42): `.proto` files of our own in
  `src/D3B.Protocol/Connection/`, which Grpc.Tools compiles into Google.Protobuf classes at build. Their numbers are the
  wire's, which the obfuscation keeps; `ReadableProtocol` lists them. The game's messages come in session 3f.
- The mapping, built in session 3d (D43 to D45), in `D3B.Protocol.Mapping`. `scripts/mapping.cs` loads the build's
  schema (`ProtocolSchema`) and pins (`src/D3B.Protocol/Pins/<version>.json`), measures anchors in the build's captures
  (`CaptureAnchors`, by structure only), pairs every readable message and enum with its counterpart (`ProtocolMatcher`:
  pins, anchors, fields, then structural scores), audits the result (`ContractAudit`), and writes it beside the pack,
  `mapping-1.binpb` (`MappingFile`), only when it holds. The mapping records the schema's SHA-256 and the readable
  protocol's digest (`ProtocolDigest`); `MappingFile.FormatVersion` changes with `mapping.proto` or that digest.
- The per-build translation turns obfuscated names and field numbers into the clean classes and back, through the
  mapping, which the host audits before it starts. An unmapped type or field is an error, logged with the raw bytes.
  Its exact form is Q2, session 3e.
- The official client logs in by itself, through Haapi and the login handshake. D3B never does (D23).
- Captures (D37): with `Network:Captures:Enabled`, each connection's raw reads go to a `.d3bcap` file in
  `%LOCALAPPDATA%\D3B\captures`, never committed. `CaptureReplay` plays one into pipes; tests build their own, and
  `scripts/capture.cs` describes them by structure. Decoded messages come from replaying them, for the protocol
  inspector and the replay tests (Q7).

## Game data

Built at step 2 (D24 to D33):

- `scripts/inspect.cs` describes the installed client, and `scripts/extract.cs` writes the pack of its build. Both read
  the client with the Unity readers copied from Dofus Remastered into `scripts/unity/` (AssetsTools.NET), the
  extraction through their typed readers, which walk an object's bytes by its type tree. The extractor checks the
  client against what D3B relies on before it writes anything. [client.md](client.md) records what the client holds,
  measured.
- A client build is named by its version (`ClientBuild`). A pack is one file per build and format, kept in
  `%LOCALAPPDATA%\D3B\data\<version>\data-<format>.pack` (`PackLocations`).
- A pack holds named entries, each a MemoryPack payload compressed with zstd, and an index at the end (D25).
  `PackWriter` writes it, compressing several entries at once in a deterministic order (D28). `GamePack` opens it by
  reading the index, then reads each entry when asked, from any thread. `PackCache` keeps the entries last read within
  a budget. A test fails when the serialized layout changes without a new `GamePack.FormatVersion`.
- A pack of format 3 holds (D27, D29):
  - every map as its own entry, its neighbours, cells and interactive elements (`MapData`), read through a
    `PackCache` by `GameMaps`;
  - the client's world graph, a vertex per zone of a map and transitions between them (`WorldGraph`), and the atlas
    of map positions, sub-areas with their resources, areas and zaaps (`WorldAtlas`), both read once by `GameWorld`;
  - the game tables a harvesting and banking bot uses: items and their types (`ItemCatalog`), jobs and skills
    (`JobCatalog`), interactives (`InteractiveCatalog`), banks and bankers with the replies that open the bank
    (`BankCatalog`), each read once by `GameTables`;
  - in French and English, the texts these name (`GameTexts`).
- Which interactive an element of a map is, and which skills it offers, the client's data does not say: the server
  does, when a character enters the map. The data links a profession's skill to its resource, and the resource to the
  sub-areas listing it.
- Packs, and from step 3 schemas and mappings, are assets of this repository's releases, one release per client
  build, tagged `client-<version>`, each asset with its SHA-256 beside it, never committed (D10, D11, D24, D39).
  `ClientRelease` names them; `scripts/release.cs` publishes a build's pack, never replacing one already published
  (D33).
- The data module (`DataModule`, in `D3B.Bot`, D31) registers, as singletons every bot shares, the installed
  `ClientBuild`, the `PackSource` that finds its pack, the `ReleaseDownloader`, the `GamePack`, opened once and closed
  with the host, and the stores reading it: `GameTexts`, `GameMaps`, `GameWorld` and `GameTables`. The configuration
  names the client (`Client:Directory`), the packs' directory (`Data:Directory`) and the releases' repository
  (`Releases:Repository`).
- Before the host starts, `PackSource` looks for the pack of the installed build on this machine, else downloads the
  release's, checks it against its SHA-256 and that it was extracted from this build, and moves it into place; then the
  host opens it. While the repository is private, the download sends `Releases:Token`, else the GitHub CLI's token
  (D32). With no pack for the installed build, the host does not start: it says which build lacks one and how to
  extract it, and exits with code 1. The pack of another build is never used.

## Desktop app

- The app is a client of the host: it lists the bots, starts and stops them, edits accounts and profiles, and streams
  state, map and logs over gRPC.
- Pages of the first version: accounts and profiles, the live map with the bot's state, logs and the protocol
  inspector (D14).
- Views are thin, over CommunityToolkit.Mvvm view models. Texts are translated from the start, in French and English.

## Scripting

- Lua scripts run in LuaCSharp, one script state per bot, with async calls into the bot (D12).
- The API follows the Snowbot family: namespaces called with `:` (`global`, `map`, `inventory`, `npc`, `job`,
  `exchange`, `character`), and the script's `move()` and `bank()`, which the bot calls.
- Modules add namespaces. The owner's profession scripts in `vendor/metiers` are the acceptance test.

## Fights

- A fight is decided by simulating candidate actions and scoring them (D13). The simulator applies spell effects read
  from the game data. It lives in `D3B.Engine`, with no I/O, so it is tested and benchmarked on its own.
- Strategies, per class or per build, are modules.

## Tests

- Logic code in `D3B.Engine` and the state handlers is tested directly, with property tests for pathfinding and line of
  sight.
- Recorded sessions replay through an in-memory `Pipe` (`CaptureReplay`, D37); tests build the sessions they replay. From
  step 3e, their decoded messages and states are compared with snapshots.
- Time is controlled through `FakeTimeProvider`.
