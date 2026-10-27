# Architecture

Where the bot is going. The reasons behind each choice are in [decisions.md](decisions.md), and the order of work is
in [roadmap.md](roadmap.md). Nothing below is built yet.

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
                                                                  │ D3B.Network: Kestrel sockets (D8)
                                                                  ▼
                                                             Ankama's servers (socket mode), or the client (MITM mode)
```

The host refuses to start when the protocol mapping it loaded does not match the installed client's build (D11).

## Projects

Provisional: step 1 confirms them.

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
| `templates/` | The `dotnet new` template for a plugin. |
| `tests/`, `benchmarks/` | One test project per `src/` project that has logic; benchmarks for the codec and pathfinding. |

## Modules and plugins

- A module implements `Build(BotBuilder)`. It registers services, message handlers, activities, fight strategies, Lua
  API namespaces, settings and UI pages. It may require other modules. Adding a module type twice builds it once.
- The default group gathers the standard modules: network, protocol, data, world, movement, harvesting, bank. D3B's
  own features are modules like any other, so a plugin can replace one.
- A plugin is a folder under `plugins/` holding an assembly with modules and an identity attribute. It loads once into
  its own `AssemblyLoadContext`, which shares the host's assemblies and is not collectible (D6).
- A plugin's UI part, loaded by the app, is Q6.

## Bot run time

- Each bot is an actor: one `Channel<T>` inbox, one logical thread, no locks on its state (D9).
- The inbox receives decoded messages, timer expiries and commands from the app. Handlers update the state. Logic code
  reads immutable snapshots of it.
- Behaviors are async methods taking a `CancellationToken`. An interruption (a fight, a disconnection, full pods)
  cancels the current behavior. A recovery step runs, then the behavior resumes or the activity picks the next one.
- Every delay goes through `TimeProvider`.
- Reconnection backs off and resumes the activity where its state allows.

## Network and protocol

- Socket mode: the bot connects a `Socket`, and `SocketConnectionContextFactory` wraps it in a `ConnectionContext`.
  MITM mode, later: a Kestrel listener with a connection handler, between the client and the server (D8).
- Framing is a varint length prefix, parsed with `SequenceReader<byte>`. The envelope carries an `Any` whose type URL
  is the obfuscated message name.
- The per-build translation turns obfuscated names and field numbers into the clean classes and back. An unmapped type
  or field is an error, logged with the raw bytes. Its exact form is Q2.
- Authentication goes through Haapi over `HttpClient`. What socket mode needs to log in is Q3.
- Every session can be recorded, raw bytes and decoded messages, for the protocol inspector and for replay tests (Q7).

## Game data

- `scripts/` reads the installed client with the Unity readers copied from Dofus Remastered (AssetsTools.NET), and
  writes one pack per client build: data tables, maps and cells, world graph, texts in French and English.
- `D3B.Data` loads a pack lazily, map by map, with a cache.
- Packs and mappings are release artifacts, never committed (D10, D11). How the host fetches the right one is Q4.

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
