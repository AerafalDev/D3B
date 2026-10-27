# Roadmap

Steps in order. A step takes one or more sessions, and every new large task starts in a new session (D17). A step is
done when its code, the tests it needs and its docs are in, and every check is green. Decisions go to
[decisions.md](decisions.md). The "Settles" line of a step lists the open questions it closes.

| # | Step | Status |
| --- | --- | --- |
| 0 | Foundation: brief, decisions, architecture, roadmap | Done |
| 1 | Scaffold and modules | Done |
| 2 | Game data | To do: next |
| 3 | Protocol and connection | To do |
| 4 | Bot run time and navigation | To do |
| 5 | Desktop app, first version | To do |
| 6 | Harvesting and banking: first milestone | To do |
| 7 | Lua scripting | To do |
| 8 | MITM mode | Dropped: merged into step 3 (D23) |
| 9 | Fights | To do |
| 10 | Several accounts and groups | To do |
| 11 | Marketplace and crafting | To do |
| 12 | Treasure hunts and dungeons | To do |
| 13 | The owner's profession scripts | To do |

## 0. Foundation

Done on 2026-10-27. The references in `vendor/` and the bots sold for Dofus 3 were studied, and decisions D1 to D18
were taken one by one with the owner. No code.

## 1. Scaffold and modules

The solution and the module system, with no game logic. Done on 2026-10-27, with decisions D19 to D22.

- `global.json`, `Directory.Build.props`, `Directory.Packages.props` (central package management), `.editorconfig`
  enforcing the C# style of `CLAUDE.md`, warnings as errors, MinVer.
- The projects of the [architecture](architecture.md#projects), empty except `D3B.Core`.
- `D3B.Core`: `IModule`, `BotBuilder` on the Generic Host, module requirements and deduplication, the default group,
  plugin loading from `plugins/` (D5, D6).
- `D3B.Host` starts, builds the modules and stops cleanly.
- The plugin template in `templates/`, and one sample plugin loaded by a test.
- Test projects on xunit.v3 and Microsoft.Testing.Platform, and CI on GitHub Actions under Windows.

## 2. Game data

Settles Q4.

- The inspection and extraction tools in `scripts/`, on the Unity readers copied from Dofus Remastered (D10).
- The pack format, MemoryPack and zstd, written by the extraction tool and read by `D3B.Data`.
- What the first milestone needs: maps and cells, the world graph with zaaps and transitions, interactives, harvestable
  resources, items, professions, banks, texts in French and English.

## 3. Protocol and connection

Settles Q2, Q7, Q8 and Q9.

- The mapping pipeline: Cpp2IL dump, IDA tracer, scored matching, pinned pairs, contract audit that fails the build
  (D11). Ideas from BillBot and JondoEmu.
- The readable `.proto` files, the generated classes, the per-build translation, the version gate.
- `D3B.Network`: a Kestrel listener between the official client and the game server, varint framing, codec over
  `IDuplexPipe` (D8, D23).
- Bringing each client's connection to D3B, within D2 (Q8). The client logs in by itself.
- Sending the bot's requests through the client's connection and keeping the client consistent (Q9).
- Recording and replaying sessions.

## 4. Bot run time and navigation

- The actor, the state and its snapshots, behaviors, interruption and recovery, reconnection (D9).
- Cell pathfinding and line of sight, world pathfinding over the world graph with zaaps and havens, in `D3B.Engine`.
- Movement between maps, interactives, the bank.

## 5. Desktop app, first version

Settles Q6.

- The host's gRPC API on a named pipe, and `D3B.Contracts` (D7).
- The Avalonia app with ShadUI: accounts and profiles, the live map with the bot's state, logs and the protocol
  inspector, in French and English (D14).
- Accounts and profiles stored in SQLite, secrets protected by Data Protection.

## 6. Harvesting and banking: first milestone

One account, through its official client and D3B's MITM (D23), harvests a profile's resources across maps and banks
them when its pods are full, controlled from the app (D16). The repository may go public once it is reached (D4).

## 7. Lua scripting

Settles Q1.

- A LuaCSharp prototype first: sandboxing, async calls, errors, performance (D12).
- The API namespaces of the Snowbot family, and the `move()` and `bank()` entry points.

## 8. MITM mode

Dropped: MITM is D3B's only mode (D23), built at step 3. The number stays, so that later steps keep theirs.

## 9. Fights

Settles Q5.

- The fight simulator in `D3B.Engine`, from spell effects in the game data, and strategies as modules (D13).
- Placement, turns, line of sight, spell choice, end of fight, and fights as interruptions of other activities.

## 10. Several accounts and groups

- Many accounts in one host, each through its own official client (D23), a group with a leader and followers.

## 11. Marketplace and crafting

- Reading prices, selling, collecting sales, crafting with recipe checks.

## 12. Treasure hunts and dungeons

- Clue solving, hint NPCs, dungeon routes.

## 13. The owner's profession scripts

- The scripts in `vendor/metiers` adapted to D3B's Lua API and modules, and tested end to end: the acceptance test of
  D12.
