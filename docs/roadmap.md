# Roadmap

Steps in order. A step takes one or more sessions, and every new large task starts in a new session (D17). A step is
done when its code, the tests it needs and its docs are in, and every check is green. Decisions go to
[decisions.md](decisions.md). The "Settles" line of a step lists the open questions it closes.

| # | Step | Status |
| --- | --- | --- |
| 0 | Foundation: brief, decisions, architecture, roadmap | Done |
| 1 | Scaffold and modules | Done |
| 2 | Game data | Done |
| 3 | Protocol and connection | Done; Q10 at the next client update |
| 4 | Bot run time and navigation | Done |
| 5 | Desktop app, first version | In progress: 5a and 5b done |
| 6 | Harvesting and banking: first milestone | To do |
| 7 | Lua scripting | Dropped: routes and routines are plugins (D90) |
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

Done on 2026-10-27, with decisions D24 to D33. Settles Q4, settled by D24 in session 2a.

- The inspection and extraction tools in `scripts/`, on the Unity readers copied from Dofus Remastered (D10).
- The pack format, MemoryPack and zstd, written by the extraction tool and read by `D3B.Data`.
- What the first milestone needs: maps and cells, the world graph with zaaps and transitions, interactives, harvestable
  resources, items, professions, banks, texts in French and English.

Four sessions:

| Session | Content | Status |
| --- | --- | --- |
| 2a | Foundations: `scripts/unity/`, `inspect.cs`, the pack format in `D3B.Data` with its tests, `extract.cs` writing the texts, Q4 (D24 to D26). | Done on 2026-10-27 |
| 2b | Maps and the world graph: Dofus Remastered's typed readers, cells, interactive elements placed on maps, coordinates and sub-areas, the client's `world-graph`, zaaps, maps read one by one through `PackCache`, pack format 2 and its layout test, compression measured (D27, D28). | Done on 2026-10-27 |
| 2c | Game tables: items and their types, jobs and skills, interactives, sub-areas' resources, banks and bankers, only the texts they name, pack format 3 at 4.7 MB, the zstd dictionary measured again (D29, D30). | Done on 2026-10-27 |
| 2d | Q4 in code: the host finds the pack of the installed build or downloads it from its release, checks it and does not start without it, the data module in `DefaultModules`, the token from the configuration or the GitHub CLI, `scripts/release.cs` publishing a build's release (D31 to D33). | Done on 2026-10-27 |

## 3. Protocol and connection

Done on 2026-10-28, with decisions D34 to D63. Settles Q2, Q7, Q8 and Q9. Q8 is settled by D34 in session 3a, whose
`hosts` line D35 replaces in session 3b, and whose game connection D52 matches in session 3f; Q7's format by D37, in
session 3b, and its replays with injection by D63, in session 3h; Q2 by D47, in session 3e; Q9 by D60, in session 3h.
Q10, how a build's game is mapped from the previous build's, waits for the next client update, which gets a session of
its own before anything else (D54, D56), and measures again that the client follows the actions D3B sends (D60).

- The mapping pipeline: Cpp2IL dump, scored matching, pinned pairs, anchors from captures, contract audit that fails the
  build (D11), and the IDA tracer if a build needs it (D41). Ideas from BillBot and JondoEmu.
- The readable `.proto` files, the generated classes, the per-build translation, the version gate.
- `D3B.Network`: a Kestrel listener between the official client and the game server, varint framing, codec over
  `IDuplexPipe` (D8, D23).
- Bringing each client's connection to D3B, within D2 (Q8). The client logs in by itself.
- Sending the bot's requests through the client's connection and keeping the client consistent (Q9).
- Recording and replaying sessions. Captures hold an account's session and are never committed: tests replay sessions
  they build themselves.

Eight sessions, the order chosen by the owner. The relay comes before the mapping, so that the client is seen going
through D3B early, the framing is measured on real bytes, and the matching has real captures for its anchors. The
connection phase is mapped before the game, whose anchors need captures of the game connection (D41), and translated
and gated before the game connection is taken, whose server address must be decoded to be rewritten (D46).

| Session | Content | Status |
| --- | --- | --- |
| 3a | Q8: how each client's connection reaches D3B, measured on running clients, and decided: a `hosts` line, and the game server's address rewritten (D34). | Done on 2026-10-27 |
| 3b | `D3B.Network` as a relay: Kestrel on D3B's loopback address and the connection to the server; the client brought to D3B by its own configuration, rewritten by the host while it runs, instead of a `hosts` line; the framing measured on real bytes, varint protobuf in the clear, and `FrameReader` with its benchmark; raw captures, their replay through a `Pipe`, and the tools checking the client and describing captures (D35 to D38). Connection phase only: the game connection still goes straight to the server. | Done on 2026-10-27 |
| 3c | Mapping, part 1: Cpp2IL reads metadata version 39; `scripts/schema.cs` writes each build's obfuscated schema, a `FileDescriptorSet` beside its pack, the structure read from the metadata and every field's encoding from the machine code; `scripts/capture.cs --schema` finds the connection's root message in 3b's captures, all their fields typed (D39, D40). | Done on 2026-10-27 |
| 3d | Mapping, part 2, the connection phase end to end: its readable `.proto` files in `D3B.Protocol` and their generated classes; the matching from pins, anchors measured in 3b's captures, fields and structural scores; the pins of 3.7.12.12, none needed; the contract audit; the mapping, a protobuf message of our own, and `scripts/mapping.cs` writing it beside the build's pack; every readable message, field and value of the connection paired (D41 to D45). | Done on 2026-10-27 |
| 3e | Q2 settled: the readable classes decode the wire once, a build translated only when its mapping holds and keeps the wire's numbers, a frame understood only when it encodes back to its length; the version gate: the protocol module finds the build's schema and mapping beside its pack, else downloads them from its release, audits them, and the host does not start without a mapping that holds; `SchemaFile` shared in `D3B.Protocol`; the release tool publishing the pack, schema and mapping together; the relay reading frames for an observer, which decodes the connection phase and logs it by structure (D46 to D50). | Done on 2026-10-28 |
| 3f | The game connection, measured on running clients: the client takes an address for the game server's host, connects to the first port it is sent whatever its connection port, 443 included, and speaks first, its first frame presenting its ticket; the relay holding the server selection's answer alone until whole, with a way out, and rewriting it once understood whole to D3B's game listener; the game connection matched with its server by its ticket, read by the wire format alone, relayed unread and captured apart from the connection phase; two accounts at once in game through D3B (D51 to D53). | Done on 2026-10-28 |
| 3g | Mapping, part 3: the game's structure measured, most of the first milestone's messages left open by it; pins for what it leaves open, which the mapping tool proposes from captures of the player's actions, build to build left for the next client update (Q10); the readable `.proto` files of the first milestone's game messages, requests whole and events partial, with the `Any` envelope and its type URLs: the game connection, the character choice, the context, the map and its actors and elements, movements, harvesting, the inventory, professions, NPC dialogs and the bank; the game's anchors and the messages its frames carry; mapping format 2; the translation reading game frames through their type URL; no IDA tracer, every field read being measured (D54 to D59). | Done on 2026-10-28 |
| 3h | Q9, measured on the captures then on a client in game through the host: every request of the first milestone carries the id -1, the client counting others alone; D3B sends only the requests that start an action, and the client follows the server's answers as after its own, confirming walks and asking for new maps' information, the player seeing each action; the relay sending the server D3B's frames between the client's, recorded in captures of format 2; the game connection read through the translation by a game session per connection, for the modules observing it; replays with injection, which complete Q7 (D60 to D63). | Done on 2026-10-28 |

## 4. Bot run time and navigation

Done on 2026-10-28, with decisions D64 to D89; D90, taken in session 4f, drops step 7.

- The actor, the state and its snapshots, behaviors, interruption and recovery, reconnection (D9), fed by game
  observers and acting through game sessions (D60, D62); how the bot and the player share the character.
- What the server answers D3B's requests, its refusals named first in the readable protocol (D55, D60).
- Cell pathfinding, world pathfinding over the world graph with doors, zaaps and havens, in `D3B.Engine`; line of sight
  at step 9, since using an element does not depend on it (D75).
- Movement between maps, interactives from the nearest cell the character reaches, the bank (D60).

Six sessions, the split chosen by the owner (D64). A client update before the next session gets Q10's session first
(D54).

Noted by the owner for later, on 2026-10-28: a large effort on criteria, so that what quests, breeds and the server's
codes gate opens to the bot (Q12); what an account without a subscription may do, told apart from what a subscribed one
may, beyond the sub-areas the world path already leaves out (D72), teleporting among it (D83). Then, in session 4e: a
plugin that records in SQLite, as the character enters each map, its NPCs with their ids, questions and replies, and in
a fight the fight's map with its red and blue cells, which the owner believes only the server's fight messages tell
apart; it needs what a plugin sees of the bots (Q11), step 5's storage and step 9's fight messages, the pack's cells
marked red and blue not being measured yet (client.md). A fight has a map of its own, of the pack and off the world
graph (D88).

Left by step 4 for later: why the client at times does not follow a walk, reproduced in session 4f on a map action's
cell that cuts a scroll's walk, about one walk in two there, each costing a journey 3.5 s (D85, D89); a fight lost,
and one an aggression begins during a step, not measured (D88).

| Session | Content | Status |
| --- | --- | --- |
| 4a | The actor and walking on a map: one bot per character of an account, its account known by the ticket the connection phase gave (D65); its inbox fed by an `IGameObserver`, the character's state and its snapshots, behaviors and their expectations (D67); the player winning, the bot pausing until resumed (D66); cell geometry and paths in `D3B.Engine`, measured on the captures (D68); the walk, over once the server answers the client's confirmation, its refusal and the player's stop named, mapping format 3, measured on a client through the host (D69); states compared with snapshots written in the tests (D70). | Done on 2026-10-28 |
| 4b | Between maps: paths through the world in `D3B.Engine`, Dijkstra's over the graph's vertices through scrolls, scrolls with an action and map actions, each change costing the same, their criteria read and answered on three values, strictly, and its benchmark (D71); the account's subscription from its identification, prudently, closing subscribers' sub-areas (D72); changing maps from the cells the client marks, the bot waiting for the new map, what is on it, then half a second for the client to draw it, the server's refusals measured silent so none named, mapping format 3 kept (D73); travelling, the path found again after every change, a failed change left out (D74); measured on a client through the host. | Done on 2026-10-28 |
| 4c | Interactives: the map's own elements in the state, those of the neighbouring maps it also lists left out, and the use the character is in; using an element from the cell the client would, until the use is over, a harvest's end measured; the server's range, line of sight measured not to count, so left at step 9; the refusals named and pinned, mapping format 4 (D75, D78); doors of skill 184 in the path through the world, their criteria strict, what each unknown code would open measured for Q12, no new fact, the owner's choice (D76); every request held until the client drew the map, a use sent too soon measured to stop it confirming walks (D77); measured on a client through the host. | Done on 2026-10-28 |
| 4d | The bank: the map's NPCs, the dialog and the bank's exchange in the state; the banks' insides from the world graph, the nearest one sought, an inside the server shows holding no banker remembered by the host for its bots, the owner's choice (D79); talking to the banker from where the character stands, his reply, the stacks in one request and the kamas in another, leaving, the refusals measured, one named and pinned, mapping format 5, and walking busy during a dialog (D80, D82); the player's leaving told from the client's by the dialog it is sent in (D81); measured on a client through the host. | Done on 2026-10-28 |
| 4e | Zaaps and havens: the player using zaaps, the haven bag and the map measured, and the bot's requests; the zaaps known, the destinations and their costs, teleporting and the haven bag named and pinned, the refusals texts of the error kind, mapping format 6 (D84); the path through the world teleporting from every map that allows it to every zaap known, the cost carried, subscribed accounts only, the haven bag only left, the owner's choice; the zaaps known, a zaap's dialog and the haven bag in the state (D83); the walk the client did not follow measured again, and the state taking the server's cell, the owner's choice (D85); measured on a client through the host. | Done on 2026-10-28 |
| 4f | Activities, interruption, recovery and reconnection: the floors cell paths ignored, measured on the server and counted on maps with stairs (D86); activities of steps chosen from the state, written by modules or plugins, with full pods unloaded before a step, the owner's choice, and a bot's state waited for by others, for step 10's groups (D87); a fight as an interruption, its context named, until step 9 fights it (D88); the recovery, the client's new connection taking the bot back, a walk the server holds let go of, and no walk of no step, which the server closes the connection of (D89); plugins rather than Lua, the owner's choice (D90); measured on a client through the host. | Done on 2026-10-28 |

## 5. Desktop app, first version

Settles Q6 and Q11: both settled in session 5b, by D96 and D95.

- The Avalonia app with ShadUI, which runs the bots in its process (D91): accounts and profiles, the live map with the
  bot's state and its activity, started, stopped and resumed from it (D87), logs and the protocol inspector, in French
  and English (D14).
- Accounts and profiles stored in SQLite; Data Protection once a secret is stored (D92).

Five sessions, the split chosen by the owner (D92), after the owner chose the app over the host's gRPC API (D91). A
client update before the next session gets Q10's session first (D54).

| Session | Content | Status |
| --- | --- | --- |
| 5a | The app with the bots in it: the bots' host in the app's process, its composition shared with `D3B.Host`, which keeps its entry point alone, and no gRPC nor `D3B.Contracts`, the owner's choice (D91); the split of step 5 (D92); the catalogue of the activities modules register, which the app starts (D93); Avalonia 12, ShadUI and Inter, dark by default, the owner's choice, the host's state and why it did not start, the bots page as a card per bot, whole, with its position, pods, kamas, pause and activity, started, stopped and resumed, after the owner turned down three panes, French and English switched live, the window read every 250 ms, and the window drawn in tests (D94); measured on a client through the app. | Done on 2026-10-29 |
| 5b | What a plugin sees and declares: Q11 settled, `D3B.Bot` packed with the libraries it names, which a plugin references, the owner's choice (D95); Q6 settled, a module's page registered with `AddPage`, made by the app from a factory naming no UI type, given the host's services, the app's language, the plugin's own texts and the window's refresh, in a rail on the left, the owner's choice among three mockups, a failing page replaced by its error, and activities and pages named in the app's language (D96); the plugin's root module read from its metadata, so that a plugin with a page loads in a host without the app (D97); the sample plugin's stroll and page, drawn in both languages and themes; the template with an activity and a page, `--page false` without; CI packing the six packages and building both. | Done on 2026-10-29 |
| 5c | Accounts and profiles in SQLite, EF Core and migrations, and their pages; whether Data Protection has a secret to protect. | To do |
| 5d | The live map, read from the pack by the app: cells, the character, its path, elements and NPCs. | To do |
| 5e | Logs kept in memory and their page; the protocol inspector, a bot's game frames decoded live, the ticket hidden, or replayed from a capture. | To do |

## 6. Harvesting and banking: first milestone

One account, through its official client and D3B's MITM (D23), harvests a profile's resources across maps and banks
them when its pods are full, controlled from the app (D16). The repository may go public once it is reached (D4).

- Noted by the owner for later: an option to bank into the guild's chest instead, when the character's guild has one
  and the character the rights.

## 7. Lua scripting

Dropped: the owner wants no Lua, and routes and routines are plugins, their activities `BotActivity`s (D87, D90). The
number stays, so that later steps keep theirs. Q1 went with it.

## 8. MITM mode

Dropped: MITM is D3B's only mode (D23), built at step 3. The number stays, so that later steps keep theirs.

## 9. Fights

Settles Q5.

- The fight simulator in `D3B.Engine`, from spell effects in the game data, and strategies as modules (D13).
- Strategies for every breed, a large effort the owner noted: the references in `vendor/` give ideas for each, to be
  done better and thought through, never copied (D3).
- Placement, turns, line of sight, spell choice, end of fight, and fights as interruptions of other activities.

## 10. Several accounts and groups

- Many accounts in one host, each through its own official client (D23), a group with a leader and followers.

## 11. Marketplace and crafting

- Reading prices, selling, collecting sales, crafting with recipe checks.

## 12. Treasure hunts and dungeons

- Clue solving, hint NPCs, dungeon routes.

## 13. The owner's profession scripts

- The scripts in `vendor/metiers` turned into plugins, their routes, harvests and banking as activities (D87, D90), and
  tested end to end.
