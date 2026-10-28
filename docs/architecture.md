# Architecture

Where the bot is going. The reasons behind each choice are in [decisions.md](decisions.md), and the order of work is in
[roadmap.md](roadmap.md). Built so far: the solution, the module and plugin model in `D3B.Core`, and a host that builds
its modules, starts and stops (step 1); the data pack format in `D3B.Data`, the tools that inspect the client, extract
its maps, world graph and game tables with the texts they name, and publish a build's pack, and the data module, whose
pack the host finds or downloads before it starts (step 2); the network module, whose relay takes each client's
connection phase through D3B, with its framing measured and its captures (step 3, session 3b); the tool writing each
build's obfuscated protocol schema, read from the client's code (step 3, session 3c); the readable protocol of the
connection phase, and the tool matching it with a build's schema into the build's mapping, audited (step 3, session 3d);
the translation of the connection phase, the version gate, the release of schemas and mappings, and the relay decoding
the connection phase (step 3, session 3e); the server selection's answer rewritten, and the game connection matched with
its server by its ticket, relayed and captured apart (step 3, session 3f); the readable protocol of the game's first
messages, its matching with pins the tool proposes, and the translation of game frames through their type URL (step 3,
session 3g); the game connection read through the translation for the modules that observe it, and D3B's requests sent
to the game server between the client's frames, recorded in the captures and replayed in tests (step 3, session 3h); the
bots' run time, one actor per character a game connection chose, fed by the game it observes, with the character's state
and its snapshots, the player taking the character over, and the first behavior, walking on a map along cell paths found
in `D3B.Engine` (step 4, session 4a); paths through the world in `D3B.Engine`, their criteria answered on three values,
the account's subscription, and the behaviors changing maps and travelling from map to map (step 4, session 4b); the
map's elements in the state, using them until the use is over, doors in the path through the world, and requests held
until the client drew the map (step 4, session 4c); the map's NPCs, dialogs and the bank's exchange in the state, the
journey to a banker, banking, and the player's leaving told from the client's (step 4, session 4d). Everything else
below is the target.

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
                                      address (D35, D51)     (D8, D23, D36, D52)
```

The host refuses to start when the protocol mapping it loaded does not match the installed client's build (D11), built in
session 3e (D48).

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
  gathers the standard modules: so far the data module (D31), the protocol module (D48), the network module (D36) and
  the bots module (D67), then, as the steps write them, world, movement, harvesting, bank. D3B's own features are modules like any other, so they can be disabled, and later replaced
  (D20).
- A plugin is a folder under `plugins/` holding an assembly named after it, whose one public class marked
  `[Plugin("id")]` is its root module. It loads once into its own `AssemblyLoadContext`, which asks the host's context
  first and is not collectible (D6). An invalid plugin stops the host from starting.
- `D3B.Host` adds `DefaultModules`, then the plugins next to its executable (or under `Plugins:Directory`), logs what
  it built, gets the pack of the installed client's build ready (Game data, below), then its protocol (Network and
  protocol, below), and runs until it is stopped.
- A plugin compiles against the `D3B.Core` package; `dotnet new d3b-plugin` creates one (D22).
- A plugin's UI part, loaded by the app, is Q6.

## Bot run time

Built in session 4a (D65 to D69), in `D3B.Bot`: the actor, the state, the sharing with the player and the first
behavior; in session 4b (D72 to D74), the subscription and the behaviors between maps; in session 4c (D75 to D77), the
map's elements, using them, doors and the drawing of the map; in session 4d (D79 to D81), the bank. Zaaps and havens are
session 4e's; activities, recovery and reconnection are 4f's (D64).

- **One bot per character** of an account (D65). `Bots`, in `D3B.Bot.Runtime`, registered by the bots module
  (`BotsModule`) as an `IGameObserver`, ties a game connection to the bot of the character its server confirms the
  client chose, its account the one the connection phase identified by the ticket (`GameSession.Account`, D65). A bot
  outlives its connections: a new one takes the old one's place, the state starting over from what the server sends.
- **The actor**, `BotActor`: one `Channel<T>` inbox read by one loop, no lock on its state (D9). The inbox gets every
  message of its character's game connection, both sides, from the relay's read loops; the loop turns each into the next
  state and tries it on the behaviors' expectations. Commands from the app come at step 5.
- **The state**, `CharacterState` in `D3B.Bot.Characters`: an immutable snapshot of what the server said of the
  character, its name and level, map, sub-area, cell and the path it walks, the map's own interactive elements and the
  use the character is in (D75), the map's NPCs, the dialog with one and the exchange with the storage (D80), inventory,
  kamas, weight and professions, and when its account's subscription ends, which the connection phase's identification
  said (D72).
  `CharacterEvents` turns each message into the next snapshot, pure functions; logic code reads snapshots.
- **Behaviors** are async methods given a `BotContext`, the state, the pack's maps, world paths and banks, the clock and
  the game connection to send through, and run one at a time by `BotActor.RunAsync`. A request waits until the client
  drew the map, half a second after the server placed the character (D77). A behavior expects its answer before it sends
  its request (`Expect`), so the loop sees the expectation before any answer. An interruption cancels it, off the loop:
  the player acting, the connection closing, the host stopping (`BotInterruptedException`).
- **The player wins** (D66): a request the player sends that acts on the character (`PlayerActions`) interrupts the bot's
  behavior, and the bot pauses until it is resumed. What the client sends by itself after an action is not the player's:
  a request to leave is the player's only while the character is in a dialog or an exchange (D81).
- **Walking on a map** (D69): `MapWalk`, in `D3B.Bot.Movement`, finds the path in the pack's cells, sends its turning
  points, and is over once the server answers the client's confirmation; a refusal, no answer or no confirmation in time
  ends it too, and the character walking already makes it busy.
- **Using an element** (D75): `ElementUse`, in `D3B.Bot.Interactives`, walks to the nearest cell the client uses the
  element from, asks for the skill by the id the server gave it on the element, and waits for the start or the refusal,
  then for the end of a use that lasts, such as a harvest. Nothing is sent for an element the map does not offer the
  skill on; the character walking or using answers `Busy`.
- **Changing maps** (D73, D76): `MapChange` follows one step of a path through the world. It walks to the nearest cell
  the map is left from, then asks for the neighbour, walks onto a map action's cell, or uses a door, and waits for the
  new map, then for what is on it, which the client asks for. The server answers nothing to a change it refuses, which
  ends after its delay, and refuses a door's use with an event.
- **Travelling** (D74): `WorldTravel` finds the path to the maps sought, follows its first step, and finds it again from
  where the character stands, until it arrives. A change that fails leaves its transition out of the journey, which
  fails after three, or when it goes on too long. The criteria are answered on the state (`WorldTravel.FactsOf`).
- **Banking** (D79, D80): `Banking`, in `D3B.Bot.Banks`, goes where a banker stands, the nearest bank's inside still
  sought, talks to him, gives the reply of his the question offers, puts the stacks asked for in by one request and the
  kamas by another, and leaves. `BankDirectory`, one per host and shared by its bots, holds the banks' insides
  (`WorldInsides`), the bankers and their replies, and the insides the server showed holding no banker. Walking, using,
  changing maps and travelling answer `Busy` during a dialog or an exchange, which the server refuses a walk during.
- Every delay goes through `TimeProvider`.
- Later in step 4: an interruption (a fight, a disconnection, full pods) cancels the current behavior, a recovery step
  runs, then the activity resumes or picks the next one; when the client reconnects, the activity resumes where its state
  allows.

## Network and protocol

Built in session 3b (D35 to D38): the relay of the connection phase, in `D3B.Network`, registered by the network module
(`NetworkModule`, in `D3B.Bot`, after the data and protocol modules, and requiring the protocol module, D48); in session
3f, the game connection (D51 to D53); in session 3h, the game read and acted on (D60 to D62).

- MITM only (D23): Kestrel takes each official client's connection, and D3B opens the matching connection to the server.
  Both sides are a `ConnectionContext` (D8). `RelayServer` runs Kestrel's server as a hosted service, with no web host,
  on `Network:Address` (127.0.13.59): `Network:Ports` (5555 and 443) for the connection phase, `Network:Game:Ports`
  (5556) for the game, each endpoint giving the relay its route (`RelayRoute`). A port it cannot take stops the host
  (D36, D53).
- How a client reaches D3B (D35): the player starts it from the official launcher. While the host runs, `ClientRedirect`
  points the client's `ConfigUrl` at a copy of Ankama's configuration whose connection host is D3B's address, a `file:`
  URL, and gives the client Ankama's back when the host stops; it follows the launcher rewriting the file.
  `scripts/client.cs` checks it and restores it after a crash. No `hosts` line, no elevation.
- `RelayConnectionHandler` finds the connection's server through `UpstreamLocator`: for the connection phase, the
  connection host Ankama's configuration names, on the port the client chose; for the game, the server whose ticket the
  client presents. It relays the bytes as they are, both ways, any number of clients at once. A side closing closes the
  other. With an `IRelayInterceptor` in the services, it asks it for each connection's `IConnectionInterceptor`, showing
  it the connection (`IRelayConnection`), reads each side as frames, shows it each whole one, and holds the frames it
  asks for until whole, forwarding what it gives in their place; a frame not whole within 5 s, or longer than 64 KiB,
  goes on as it came, and a side that is no frames is watched no more (D49, D51). Through the connection, D3B sends the
  server frames of its own: each goes between two of the client's frames, at once or once the client's frame in
  progress has gone whole, and fails when the connection closes or its client's bytes are no frames (D61). Nothing is
  ever sent to the client.
- The game connection (D51 to D53): the connection phase's interceptor has the server's frames held from the client's
  server selection to its answer, and rewrites a success understood whole, its host to D3B's address and its ports to
  the game listener's, keeping the real game server and the ticket in `GameServerSelections`. The client speaks first on
  the game connection, its first frame presenting the ticket: the game listener reads that frame before connecting
  anywhere, finds the selection waiting for its ticket among the frame's values by the wire format alone
  (`ProtobufValues`), and connects to that selection's server. A connection presenting no ticket waiting is closed.
- The game, read (D62): `RelayDecoder`, the relay's interceptor, gives each game connection a `GameSession`, which
  decodes every frame through the translation (`DecodeGame`, D58), shows it to every `IGameObserver` in the services,
  and logs it at the debug level by what it carries, never by what it holds. It holds and rewrites no frame of the
  game. An observer sees each session start, then each frame of both sides, from the relay's read loops: the bots post
  what they see to their inboxes (D9, D67). A game session knows its account once the client's first request presented
  the ticket the connection phase gave it (`GameAccounts`, D65).
- The client's other connections, to the launcher, Haapi, the chat and the CDN, do not go through D3B.
- The bot acts through that connection (D60): `GameSession.SendAsync` sends the server a readable request, encoded
  whole by `ProtocolTranslation.EncodeGameRequest` with the id -1 that every request of the first milestone carries,
  between the client's frames. D3B sends only the requests that start an action: walking, changing maps, using an
  element, acting on an NPC, replying, moving a stack or kamas into the storage, leaving a dialog. The client follows
  the server's answers as after its own actions, confirming a walk once arrived and asking what is on a new map, so
  D3B never sends those; the bot learns that an action is over from what the server answers.
- Framing, measured (D38): a varint length, then a protobuf message, in the clear. `FrameReader` reads frames from a
  `SequenceReader<byte>` without copying; a benchmark in `benchmarks/D3B.Network.Benchmarks` measures it. The game
  connection's envelope carries an `Any` whose type URL is `type.ankama.com/` and the obfuscated message's name,
  measured (D56); the connection phase has none.
- The obfuscated schema, built in session 3c (D39, D40): `scripts/schema.cs` reads the installed build's protocol from
  its IL2CPP code with Cpp2IL, its structure from the metadata and the encoding of every field from the machine code,
  and writes it as a `FileDescriptorSet` beside the build's pack, `schema-1.binpb`. `scripts/capture.cs --schema` reads
  captures with it, field by field.
- The readable protocol, built in session 3d for the connection phase (D42) and in session 3g for the game's first
  messages (D55, D56): `.proto` files of our own in `src/D3B.Protocol/Connection/` and `src/D3B.Protocol/Game/`, which
  Grpc.Tools compiles into Google.Protobuf classes at build. Their numbers are 3.7.12.12's wire's; the game's are likely
  shuffled at each build, which the next build measures (Q10). `ReadableProtocol` lists them. A game event marked
  `(d3b.readable.partial)` declares only the fields bot code reads; a request is whole.
- The mapping, built in session 3d (D43 to D45), in `D3B.Protocol.Mapping`. `scripts/mapping.cs` loads the build's
  schema (`ProtocolSchema`) and pins (`src/D3B.Protocol/Pins/<version>.json`), measures anchors in the build's captures
  (`CaptureAnchors`, by structure and type URLs only, the connection phase's in the captures' directory and the game's
  in its `game` directory, with the messages the game's frames carry), pairs every readable message and enum with its
  counterpart (`ProtocolMatcher`: pins, anchors, fields, then structural scores; `google.protobuf.Any` is its own
  counterpart), audits the result (`ContractAudit`, which counts what partial messages leave out apart from its notes),
  and writes it beside the pack, `mapping-5.binpb` (`MappingFile`, D82), only when it holds. For what is left open it
  lists the build's messages that fit, which the maintainer pins (D54). The mapping records the schema's SHA-256 and the
  readable protocol's digest (`ProtocolDigest`); `MappingFile.FormatVersion` changes with `mapping.proto` or that
  digest.
- The per-build translation, built in session 3e for the connection phase (D47): `ProtocolTranslation`, in
  `D3B.Protocol.Translation`, exists only for a build whose mapping holds the contract and pairs every readable field and
  value with its own number, since the readable classes read the wire as it is. A frame is decoded once and understood
  only when its message encodes back to its length; one that is not is passed on as it is, and never rewritten. A game
  frame's type URL names the build's message, which the mapping turns into the readable one (`DecodeGame`, D58); a frame
  of a partial message is read without being understood whole (D55).
- The version gate (D48): the protocol module (`ProtocolModule`, in `D3B.Bot`) registers `ProtocolSource`, which finds
  the build's schema and mapping beside its pack, else downloads them from its release, and the translation, which the
  host asks for before it starts. Without a mapping that holds, the host logs what fails and the commands mapping the
  build, `scripts/schema.cs` then `scripts/mapping.cs`, which need no capture, and exits with code 1.
- The connection phase, decoded (D49, D51): `RelayDecoder` gives each connection phase a `ConnectionPhaseSession`, which decodes every frame through the translation and logs it by the readable fields
  its oneofs hold and its length, never a value, a frame not understood being a warning, and rewrites the server
  selection's answer.
- The official client logs in by itself, through Haapi and the login handshake. D3B never does (D23).
- Captures (D37): with `Network:Captures:Enabled`, each connection's raw reads go to a `.d3bcap` file in
  `%LOCALAPPDATA%\D3B\captures`, the game connection's in its `game` directory, apart from the connection phase's that
  the mapping tool reads (D53), never committed. From format 2, the frames D3B sends the server are records of their
  own (D61). `CaptureReplay` plays one into pipes, D3B's frames into a pipe of their own when asked; tests build their
  own, and `scripts/capture.cs` describes them by structure. Decoded messages come from replaying them, for the
  protocol inspector and the replay tests (D63).

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
- Packs, schemas and mappings are assets of this repository's releases, one release per client build, tagged
  `client-<version>`, each asset with its SHA-256 beside it, never committed (D10, D11, D24, D39). `ClientRelease`
  names them; `scripts/release.cs` publishes a build's pack, schema and mapping together, once the mapping holds, never
  replacing an asset already published (D33, D50).
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

## Engine

Pure logic with no I/O, in `D3B.Engine`, which references `D3B.Data` for the cells and the world. Built in session 4a
(D68), 4b (D71, D73), 4c (D75, D76) and 4d (D79):

- `MapGeometry`, in `D3B.Engine.Maps`: 14 columns of 40 staggered rows, the eight directions between neighbours, the
  coordinates the server names a cell by (`MapPoint`), measured on the captures, and the distance between two cells, the
  sum of their coordinates' differences, by which the server measures a skill's range.
- `ElementCells`: the cells a character uses an element from, as the client chooses them, the walkable neighbours of the
  element's cell in the character's zone, else the zone's walkable cells nearest it.
- `CellPathfinder`: A* over the cells a character may walk on, a step across passing beside at least one walkable cell,
  steps weighed by the time the character takes, to one cell or the nearest of several; `CellPath` gives the turning
  points a request names. Nothing is kept between calls.
- `Criterion`, in `D3B.Engine.Criteria`: a criterion of the client's, read once into a tree and answered on three values
  from `CriterionFacts`, what the bot knows; a code it does not know is unknown.
- `WorldInsides`, in `D3B.Engine.World`: the maps a map's doors lead into at its coordinates on no world map, such as a
  bank's insides (D79).
- `WorldPathfinder`, in `D3B.Engine.World`: Dijkstra's over the world graph's vertices, a zone of a map each, through
  scrolls, scrolls with an action, map actions and doors whose criteria are met (`WorldPathRules`), each change costing
  the same, to the nearest of several maps; sub-areas closed to accounts without a subscription are entered by
  subscribed ones only. It gives the cells each map is left from (`ExitCells`), a door's around its element. Built once
  per pack and shared; a search keeps its state in its own arrays, and takes under 0.4 ms
  (`benchmarks/D3B.Engine.Benchmarks`).
- Later: zaaps and havens (4e) in the world path; line of sight and the fight simulator at step 9, using an element not
  depending on line of sight (D75).

## Fights

- A fight is decided by simulating candidate actions and scoring them (D13). The simulator applies spell effects read
  from the game data. It lives in `D3B.Engine`, with no I/O, so it is tested and benchmarked on its own.
- Strategies, per class or per build, are modules.

## Tests

- Logic code in `D3B.Engine` and the state handlers is tested directly, with property tests on CsCheck for cell and
  world paths (D68, D71), and later line of sight.
- Recorded sessions replay through an in-memory `Pipe` (`CaptureReplay`, D37); tests build the sessions they replay.
  Decoded frames are compared with the messages a test built (D49); bot states with snapshots the test holds, serialized
  as indented JSON (D70).
- Bots are tested through game connections driven as the relay shows them, on a pack of five maps, the world between
  them, a door leading into two insides, a tree and a bank, with the bots' loop waited for and the clock moved (D67).
- Replays with injection (D63): a game session a test built is replayed through the relay, read by a game session whose
  observer sends a request; the test checks what each side got, what the observers saw, and the relay's capture.
- Time is controlled through `FakeTimeProvider`.
