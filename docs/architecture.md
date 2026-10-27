# Architecture

Where the bot is going. The reasons behind each choice are in [decisions.md](decisions.md), and the order of work is
in [roadmap.md](roadmap.md). Built so far: the solution, the module and plugin model in `D3B.Core`, and a host that
builds its modules, starts and stops (step 1); the data pack format in `D3B.Data`, and the tools that inspect the client
and extract its texts (step 2, first session). Everything else below is the target.

## Overview

```text
Official client (Unity)                 scripts/ (file-based apps, D10)              release artifacts, per client build
Dofus_Data/StreamingAssets/** ────────► extraction: Unity readers ───────────────►  data pack (MemoryPack + zstd)
GameAssembly.dll + metadata   ────────► mapping: Cpp2IL, IDA tracer, matching ───►  protocol mapping
                                                                                             │ fetched and loaded by
                                                                                             ▼
D3B.App (Avalonia, ShadUI) ◄── gRPC on a named pipe (D7) ──► D3B.Host (headless: Generic Host, Kestrel)
                                                                  │ BotBuilder: default modules + plugins/ (D5, D6)
                                                                  ▼
                                                             one async actor per bot (D9)
                                                                  │ D3B.Network: Kestrel, between client and server (D8, D23)
                                                                  ▼
                                                             official client, one per account ◄──► Ankama's servers
```

The host refuses to start when the protocol mapping it loaded does not match the installed client's build (D11).

## Projects

Confirmed at step 1 (D19). Every project exists in `D3B.slnx`; those with no code yet are empty class libraries, and
each step adds the references it needs.

| Path | Role |
| --- | --- |
| `src/D3B.Core` | Modules and `BotBuilder`, plugin loading, shared abstractions. What a plugin references. |
| `src/D3B.Network` | Kestrel transport, framing, codec over `IDuplexPipe`. The only project referencing `Microsoft.AspNetCore.App`, with the host. |
| `src/D3B.Protocol` | Classes generated from the readable `.proto` files, and the per-build translation. |
| `src/D3B.Data` | Data pack format and runtime readers: maps, cells, world graph, items, spells, monsters, interactives, texts. |
| `src/D3B.Engine` | Pure logic with no I/O: cell and world pathfinding, line of sight, fight simulation. |
| `src/D3B.Bot` | The bot's run time: actor, state, behaviors, recovery, and the default modules. |
| `src/D3B.Scripting` | The Lua host and its API namespaces (D12). |
| `src/D3B.Host` | The headless host: Generic Host, gRPC server on a named pipe, storage, logs. |
| `src/D3B.Contracts` | The gRPC contracts shared by the host and the app. |
| `src/D3B.App` | The Avalonia desktop app. |
| `scripts/` | Inspection, extraction and mapping tools, as file-based apps. |
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
  will gather the standard modules: network, protocol, data, world, movement, harvesting, bank. D3B's own features are
  modules like any other, so they can be disabled, and later replaced (D20).
- A plugin is a folder under `plugins/` holding an assembly named after it, whose one public class marked
  `[Plugin("id")]` is its root module. It loads once into its own `AssemblyLoadContext`, which asks the host's context
  first and is not collectible (D6). An invalid plugin stops the host from starting.
- `D3B.Host` adds `DefaultModules`, then the plugins next to its executable (or under `Plugins:Directory`), logs what
  it built, and runs until it is stopped.
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

- MITM only (D23): a Kestrel listener with a connection handler takes each official client's connection, and D3B
  opens the matching connection to the game server. Both sides are a `ConnectionContext` (D8). How a client's
  connection reaches D3B is Q8.
- The bot acts through that connection, and the client is kept consistent with what it does: Q9.
- Framing is a varint length prefix, parsed with `SequenceReader<byte>`. The envelope carries an `Any` whose type URL
  is the obfuscated message name.
- The per-build translation turns obfuscated names and field numbers into the clean classes and back. An unmapped type
  or field is an error, logged with the raw bytes. Its exact form is Q2.
- The official client logs in by itself, through Haapi and the login handshake. D3B never does (D23).
- Every session can be recorded, raw bytes and decoded messages, for the protocol inspector and for replay tests (Q7).

## Game data

Built in session 2a (D24 to D26); sessions 2b to 2d complete it:

- `scripts/inspect.cs` describes the installed client, and `scripts/extract.cs` writes the pack of its build. Both read
  the client with the Unity readers copied from Dofus Remastered into `scripts/unity/` (AssetsTools.NET).
  [client.md](client.md) records what the client holds, measured.
- A client build is named by its version (`ClientBuild`). A pack is one file per build and format, kept in
  `%LOCALAPPDATA%\D3B\data\<version>\data-<format>.pack` (`PackLocations`).
- A pack holds named entries, each a MemoryPack payload compressed with zstd, and an index at the end (D25).
  `PackWriter` writes it. `GamePack` opens it by reading the index, then reads each entry when asked, from any thread.
  `PackCache` keeps the entries last read within a budget, for maps.
- So far a pack holds the numbered texts in French and English (`GameTexts`). Session 2b adds maps and cells, and the
  world graph with zaaps and transitions, read from the client's own `world-graph`. Session 2c adds interactives,
  harvestable resources, items, professions and banks, and keeps only the texts they name.
- Packs and mappings are assets of this repository's releases, one release per client build, never committed (D10,
  D11, D24). The host will use a local pack first, else download the release's and check its SHA-256, and will not
  start without one (session 2d).

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
- Recorded sessions replay through an in-memory `Pipe`, and their decoded messages and states are compared with
  snapshots.
- Time is controlled through `FakeTimeProvider`.
