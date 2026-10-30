# Architecture

Where the bot is going. The reasons behind each choice are in [decisions.md](decisions.md), and the order of work is in
[roadmap.md](roadmap.md). Built so far: the solution, the module model in `D3B.Core`, and a host that builds its
modules, starts and stops (step 1); the data pack format in `D3B.Data`, the tools that inspect the client, extract
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
journey to a banker, banking, and the player's leaving told from the client's (step 4, session 4d); the zaaps the
character knows and the haven bag in the state, teleporting from where the character stands in the path through the
world, leaving the haven bag, and a walk the client does not follow settled where the server has the character (step 4,
session 4e); floors in cell paths, and activities chaining behaviors, interrupted by the player, a disconnection or a
fight, the character recovered and taken back by the client's new connection (step 4, session 4f); the desktop app,
which runs the bots' host in its process, with its bots page in French and English, dark by default, and the catalogue
of the activities it starts (step 5, session 5a); activities and pages named in the app's language, and the modules'
pages in the app's rail (step 5, session 5b); activities' settings, which their modules describe in code, of every kind
and of the game's values; the accounts the relay sees and the profiles, in SQLite through EF Core, which the app and
`D3B.Host` share; and the app's accounts and profiles pages, its rail always shown (step 5, session 5c); the external
plugins removed, and Lua scripts in their place at step 7, removed since (D116, D119), an activity's error on the bot's
card staying; each bot's journal of its actions, the host's problems and each bot's game frames kept in memory, and the
app's logs page and protocol inspector, live or replaying a capture (step 5, session 5e); the live map (step 5, session
5d); the harvesting module, its activity touring a zone and banking full, an activity's report on the bot's card and the
map page, and the profile editor grown with suggestions, a zone's mini-map and a preview (step 6, session 6a); the icons of
the resources and professions in a pack of format 4, drawn on the tiles, lists, card and map, and the maps' graphics the
bots learn, which send a tour to the probable maps first (step 6, session 6b); the resources of `Base` placed by the graphics
learned, a zone's isolated maps, fights told by their timing and followed as the profile says, the resources' growing
back learned and kept, and each run kept for a statistics page (step 6, session 6c); the app redesigned (step 6, session
6d); the harvest's resources by profession, its place chosen by each character's levels, zaaps only when they save enough
maps, and profiles given (step 6, session 6e); the app published by CI with Velopack, shipping its client build's pack,
schema and mapping, and updating itself from the repository's releases, the host downloading nothing (step 6, session
6f); the fight's protocol measured and mapped, the fight's state in the bot's, and the fight on the app's map (step 9,
session 9a); the fight's data in the pack, format 5, its flags read in the client's code, and the monsters named on the
map (step 9, session 9b); the rules of a cast in `D3B.Engine`, its range, line of sight, zones, target masks, conditions,
costs and cooldowns, and walking in a fight with its tackle, checked on every cast and walk captured (step 9, session 9c).
Everything else below is the target.

## Overview

```text
Official client (Unity)                 scripts/ (file-based apps, D10)              release artifacts, per client build
Dofus_Data/StreamingAssets/** ────────► extraction: Unity readers ───────────────►  data pack (MemoryPack + zstd)
GameAssembly.dll + metadata   ────────► mapping: schema, captures, matching ─────►  schema, protocol mapping
                                                                                             │ shipped with the app (D172)
                                                                                             ▼
D3B.App (Avalonia, ShadUI) ── runs in its own process (D91) ──► the bots' host: Generic Host, Kestrel
installed and updated by Velopack from the releases (D171, D173)
D3B.Host: the same host, without the app                          │ BotBuilder: default modules, storage (D5)
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
| `src/D3B.Core` | Modules and `BotBuilder`, shared abstractions. |
| `src/D3B.Network` | Kestrel transport, framing, codec over `IDuplexPipe`. The only project referencing `Microsoft.AspNetCore.App`, with the host. |
| `src/D3B.Protocol` | Classes generated from the readable `.proto` files, a build's mapping, its matching and audit, and the per-build translation. |
| `src/D3B.Data` | Data pack format and runtime readers: maps, cells, world graph, items, spells, monsters, interactives, texts. |
| `src/D3B.Engine` | Pure logic with no I/O: cell and world pathfinding, line of sight, fight simulation. |
| `src/D3B.Bot` | The bot's run time: actor, state, behaviors, recovery, activities and their settings, pages, the default modules, and the host's composition the app and `D3B.Host` share. |
| `src/D3B.Host` | The bots' host without the app, for measurements: its entry point alone, with the database (D91, D100). |
| `src/D3B.Storage` | D3B's database, SQLite through EF Core and its migrations: the accounts seen and the profiles, which the app and `D3B.Host` share (D100). |
| `src/D3B.App` | The Avalonia desktop app, which runs the bots' host in its process (D91). |
| `scripts/` | Inspection, extraction, release and mapping tools, as file-based apps. |
| `tests/`, `benchmarks/` | One test project per `src/` project that has logic; benchmarks for the codec and pathfinding. |

## Modules

Built at step 1, in `D3B.Core` (D20); the external plugins went at step 7 (D102, D109), then the Lua scripts that
replaced them (D116, D119). Mods come as blueprints at step 7, after step 6 (Mods, below):

- A module implements `IModule.Build(BotBuilder)`. It registers services, message handlers, activities, fight
  strategies, settings and UI pages. `BotBuilder` wraps the Generic Host's builder and is an `IHostApplicationBuilder`: a
  module registers through `Services`, `Configuration` and `Logging`, and `Build()` returns the `IHost`.
- A module is built as soon as it is added. It requires another by calling `bot.AddModule<T>()` first in its `Build`.
  Adding a module type twice builds it once.
- A group (`IModuleGroup`) lists modules in order; whoever adds it may disable some. `DefaultModules`, in `D3B.Bot`,
  gathers the standard modules: so far the data module (D31), the protocol module (D48), the network module (D36), the
  bots module (D67) and the harvesting module (D127), then, as the steps write them, fights and the rest. D3B's own
  features are modules like any other, so they can be disabled, and later replaced (D20).
- The host's composition, `BotHost` in `D3B.Bot.Hosting`, which the app and `D3B.Host` share (D91), adds
  `DefaultModules`, logs what it built, gets the pack of the installed client's build ready (Game data, below), then its
  protocol (Network and protocol, below), and starts the host, or says why it did not (`BotHostStart`). The app and
  `D3B.Host` add the storage module (D100). `D3B.Host` then runs until it is stopped; the app, until its window is
  gone.
- A module's page (D96), registered with `AddPage` (`D3B.Bot.Pages`) and listed by the bots module in the
  `PageCatalog`, is an Avalonia control the app makes, in its process (D91), from a factory: the contract names no type
  of the UI's framework, and a host without the app never makes a page. The page gets a `PageContext`: the host's
  services, the app's language and the module's texts in it (`PageTexts`), and the window's refresh.
- Texts the app shows for a module, a page's title or an activity's name, are `LocalizedText`s (`D3B.Bot.Localization`):
  the same in every language, or the module's own resources, in English and French (D96).
- An activity's settings (D98), in `D3B.Bot.Settings`: a class whose initial values are the defaults, which the module
  describes in code as it registers the activity, `AddActivity<TActivity, TSettings>(id, name, settings => ...)`, each
  property a setting of a kind, a flag, a number, a text, a choice or the game's professions, areas, sub-areas and
  resources, one or a list, with its name, bounds and help; lists ordered or not, values sought within another setting's,
  settings shown under a condition, sections and rules; sub-areas suggested by a resource setting, percentages, and a
  preview the activity reckons from its settings and the pack, figures and the maps of its zone (D127); and, since 6e, what
  an earlier version stored read once the rest is (`ReadsEarlier`), the professions that gather, `Base`'s among them
  (`Gathering`), a list of resources leaving out (`Excluding`), sub-areas suggested by resources the activity reckons, a
  number's unit, a preview telling each character playing the settings what they lead to for it (`PreviewPlayer`,
  `PreviewPlayerNote`), and presets, settings ready to play that D3B stores once as profiles (`Preset`, D165 to D168).
  `ActivitySettings` reads and writes them as JSON and checks them, with the game's values (`GameReferences`) once the pack
  is there; the activity receives them through its constructor.
- The activities the app starts are listed by the `ActivityCatalog`: the modules' activities, fixed once the host is
  built (D93); D3B's first is the harvesting module's (D127).
- A module may get something ready before the host starts, such as its database, through an `IHostPreparation`: the
  first that fails stops the host, which says why (D100).

## Bot run time

Built in session 4a (D65 to D69), in `D3B.Bot`: the actor, the state, the sharing with the player and the first
behavior; in session 4b (D72 to D74), the subscription and the behaviors between maps; in session 4c (D75 to D77), the
map's elements, using them, doors and the drawing of the map; in session 4d (D79 to D81), the bank; in session 4e (D83,
D85), teleports and the haven bag; in session 4f (D87 to D89), activities, fights as interruptions, recovery and the
client's new connection taking the bot back; in session 6a (D127 to D132), harvesting and an activity's report; in session 6c
(D144 to D146, D149, D150), fights while harvesting, the growing back learned, runs' figures, isolated maps and Base's
resources; in session 9a (D179), the fight's state.

- **One bot per character** of an account (D65). `Bots`, in `D3B.Bot.Runtime`, registered by the bots module
  (`BotsModule`) as an `IGameObserver`, ties a game connection to the bot of the character its server confirms the
  client chose, its account the one the connection phase identified by the ticket (`GameSession.Account`, D65). A bot
  outlives its connections: a new one takes the old one's place, the state starting over from what the server sends.
- **The actor**, `BotActor`: one `Channel<T>` inbox read by one loop, no lock on its state (D9). The inbox gets every
  message of its character's game connection, both sides, from the relay's read loops; the loop turns each into the next
  state and tries it on the behaviors' expectations. Commands from the app come at step 5.
- **The state**, `CharacterState` in `D3B.Bot.Characters`: an immutable snapshot of what the server said of the
  character, its name and level, map, sub-area, cell and the path it walks, the map's own interactive elements and the
  use the character is in (D75), the map's NPCs, the dialog with one and the exchange with the storage (D80), the zaaps
  it knows, a zaap's dialog with its costs and the haven bag it is in (D83), whether it fights (D88), whether the server
  may still hold a walk the client did not follow (D89), inventory, kamas, weight, how full it is, and professions, and
  when its account's subscription ends, which the connection phase's identification said (D72), and its fight (D179).
  `IsBusy` gathers what makes every behavior answer `Busy`: a walk, a use, a dialog or exchange, a fight.
  `CharacterEvents` turns each message into the next snapshot, pure functions; logic code reads snapshots.
- **Behaviors** are async methods given a `BotContext`, the state, the pack's maps, world paths and banks, the clock and
  the game connection to send through, and run one at a time by `BotActor.RunAsync`. A request waits until the client
  drew the map, its display time after the server placed the character, half a second measured (D77, D151). A behavior
  expects its answer before it sends its request (`Expect`), so the loop sees the expectation before any answer. An
  interruption cancels it, off the loop:
  the player acting, the connection closing, a fight beginning (D88), the host stopping (`BotInterruptedException`).
  Anyone may wait for what a bot's state becomes (`BotActor.WaitUntilAsync`), as a follower will at step 10, and a
  behavior for its own state, with a delay (`BotContext.WaitUntilAsync`).
- **The player wins** (D66): a request the player sends that acts on the character (`PlayerActions`) interrupts the bot's
  behavior, and the bot pauses until it is resumed. What the client sends by itself after an action is not the player's:
  a request to leave is the player's only while the character is in a dialog or an exchange (D81).
- **Walking on a map** (D69): `MapWalk`, in `D3B.Bot.Movement`, finds the path in the pack's cells, sends its turning
  points, and is over once the server answers the client's confirmation; a refusal, no answer or no confirmation in time
  ends it too, and the character walking already makes it busy. A walk the client does not confirm leaves the character
  on its last cell, where the server has it (`BotContext.SettleWalkAsync`, D85); the server may still hold it, and
  refuses what is not a walk from there, so a step aside comes first (`MapWalk.ReleaseHeldWalkAsync`, D89). A walk of no
  step is never sent: the server closes the connection of one.
- **Using an element** (D75): `ElementUse`, in `D3B.Bot.Interactives`, walks to the nearest cell the client uses the
  element from, asks for the skill by the id the server gave it on the element, and waits for the start or the refusal,
  then for the end of a use that lasts, such as a harvest. Nothing is sent for an element the map does not offer the
  skill on; the character walking or using answers `Busy`.
- **Changing maps** (D73, D76): `MapChange` follows one step of a path through the world. It walks to the nearest cell
  the map is left from, then asks for the neighbour, walks onto a map action's cell, or uses a door, and waits for the
  new map, then for what is on it, which the client asks for. The server answers nothing to a change it refuses, which
  ends after its delay, and refuses a door's use with an event. A teleport (D83) is one request from where the character
  stands, refused with an error text (D84); `MapChange.LeaveHavenBagAsync` leaves the character's haven bag, back to the
  map it was entered from.
- **Travelling** (D74): `WorldTravel` finds the path to the maps sought, follows its first step, and finds it again from
  where the character stands, until it arrives. A change that fails leaves its transition out of the journey, which
  fails after three, or when it goes on too long; a teleport that fails leaves every teleport out. A journey from the
  character's haven bag leaves it first. The criteria are answered on the state (`WorldTravel.FactsOf`), and teleports
  on the zaaps it knows and the kamas it carries (`WorldTravel.RulesOf`, D83). A teleport counts as as many map changes as
  the running activity says (`BotActivity.TeleportChanges`, `WorldPathRules.TeleportChanges`), so that a zaap is taken only
  when it saves that many, and never on a tie with a walk (D167).
- **Banking** (D79, D80): `Banking`, in `D3B.Bot.Banks`, goes where a banker stands, the nearest bank's inside still
  sought, talks to him, gives the reply of his the question offers, puts the stacks asked for in by one request and the
  kamas by another, and leaves. `BankDirectory`, one per host and shared by its bots, holds the banks' insides
  (`WorldInsides`), the bankers and their replies, and the insides the server showed holding no banker. Walking, using,
  changing maps and travelling answer `Busy` during a dialog or an exchange, which the server refuses a walk during.
- **Activities** (D87), in `D3B.Bot.Activities`: a `BotActivity`, written by a module, chooses each step
  from the state, a behavior's worth of work; its other hooks have defaults: when the character is full and how it is
  unloaded, what each interruption leads to, how the character is recovered. `BotActor.StartActivity` runs one at a
  time (`ActivityRun`): each step, unloading and recovery a behavior started once the bot can act, connected, placed,
  out of a fight and not paused, so the run waits, with no delay, for the player to resume the bot, the client's new
  connection to take it back (D65) or the fight to end (D88). A full character is unloaded before the next step.
  Nothing of the activity's position is kept: the next step starts from the state. An activity that fails keeps its
  error (`ActivityRun.Failure`), which the app shows (D103); one that is `IDisposable` is disposed once over.
- **The activities the app starts** (D93): a module registers each one, by an id unique in the host and a name in the
  app's language (D96), with `AddActivity<T>`, or `AddActivity<T, TSettings>` with its settings (D98), and the bots
  module lists them in the `ActivityCatalog`; one is made, with the host's services and its settings, for each start.
- **Recovery** (D89): before going on after an interruption, `Recovery.SettleAsync` lets a use and a walk end, leaves a
  dialog or exchange still open, and lets go of a walk the server holds, each wait bounded.
- **An activity's report** (D131): what it does, how far it is, what it counts, its warnings and what it looks at on the
  map (`BotActivity.Report`, `ActivityReport`), replaced whole and read by the app; its texts name the game's values by
  their ids (`GameText`, in `D3B.Bot.Localization`). One that cannot go on says why (`ActivityException`); a run keeps the
  name it was started under (`ActivityRun.Title`), and when it started and ended.
- **Harvesting** (D127 to D130), in `D3B.Bot.Harvesting`, registered by `HarvestingModule`: `HarvestActivity`, its settings
  `HarvestSettings` and their zone `HarvestZone`, the skills to use, the sub-areas and their maps, the insides apart. Each
  step harvests the wanted resource the server offers at the character's level, the first asked for then the nearest, or
  travels to the nearest map of the tour still to go through (`HarvestTour`): the probable maps first, then those of the
  world map, then the insides. `HarvestSightings`, one per host, remembers what each map holds; a map never seen is
  foretold by its elements' graphics (`HarvestZone.Foresee`, D138), and a tour leaves out the maps seen empty, those
  whose graphics all offer none of its skills, and those without elements. Full at its
  share of pods, it banks at the nearest bank, what the settings say, and the next step goes back; still full, it says
  why. A fight, a new connection and the player's pause resume it. `HarvestPreviews` reckons what settings lead to for the
  app. Banking waits the client's display time, half a second measured, after the banker's question and after the bank
  opens, so that the client shows its windows before D3B closes them (D132, D151).
- **Harvesting, grown in 6c**: a resource of `Base`, such as a well's water, is asked for as any other, without a level,
  and placed by the graphics learned (`GameReferences.IsBase`, `PlacesOf`; `GraphicPlaces`, an index of every map's
  graphics built once, D150); a sub-area's maps reached only far round from its main part are isolated and left out
  unless the settings take them, last (`HarvestZone.Isolated`, D149). A fight that cuts a step is a protector's when it
  begins within a second of the character's own use ending (`BotContext.UseEndedAt`), else an aggression; the activity
  then resumes, pauses the bot (`InterruptionResponse.Pause`, `BotActor.Pause`) or stops, as its settings say, and the
  journal names the kind (`BotActivity.Describe`, D144). `Regrowths`, one per host, learns how long each resource takes to
  grow back from every map a bot is on (`RegrowthLearner`): whole when a bot stayed on the map, else a bound from above on
  a later visit; a tour holds back the maps whose wanted elements all still grow back, by `HarvestSightings`' elements,
  and waits for the first rather than D128's minute (D145). Before a step, the activity opens a resource bag and, under its
  share of life, eats the profile's food in one request (`ObjectUse.UseObjectsAsync`, the life reckoned by `BotActor.LifeAt`,
  D148, D153, D156); full, it may deposit in the guild's chest (`GuildChest`, D147), what the chest leaves going to the
  character's own. Between two steps the run lets `BotDelays.BetweenSteps` pass, the client's display time at least (D154).
  An activity gives its figures (`BotActivity.Statistics`,
  `ActivityStatistics`), and a run keeps the character it started with (`ActivityRun.StartedWith`, D146).
- **Harvesting, grown in 6e**: the resources by profession, the professions chosen, `Base`'s among them, but those left
  out, the nearest first or the highest level first, or a list as before (`HarvestMode`, `HarvestOrder`,
  `HarvestZone.TargetsOf`, D165); the place chosen, or chosen by the activity for each character at each tour, the sub-area
  where the learned graphics place the most of what it can harvest at its levels, each 20 levels below a profession's
  halving a resource's worth, not above the character's level (`HarvestPlace`, `AutomaticPlace`, D166), its name before
  the tour on the card; a zaap only when it saves the profile's number of maps, 5 by default (`HarvestSettings.ZaapSaving`,
  D167). The module reads a profile stored before as it harvested, and gives six presets (D168).
- **Fights** (D179), in `D3B.Bot.Fights`: the character's state holds its fight (`CharacterState.Fight`, `FightState`) from
  the fight's context to the roleplay's: its phase, the character's team and both teams' placement cells, whether the
  character is ready, the fighters by id (`Fighter`: team, cell, life and most life, AP and MP, level, breed or monster and
  grade, summoner), the turns' order, the round, whose turn it is and whether the character may act, and how the fight
  ended for it and what it gained (`FightEnd`). `FightEvents`, pure functions, take every fight message before the
  character's do, so that a fighter's walk, the character's among them, moves it with no path to confirm; AP and MP come from
  the characteristics alone, life from the losses and heals announced. The journal writes a fight's end once
  (`FightEndEntry`). The client acknowledges the sequences and says it is ready for each turn by itself; the bot sends
  nothing in a fight until 9e.
- **The maps' graphics** (D138), in `D3B.Bot.Interactives`: `GraphicSkills`, one per host, holds what the server said the
  elements of each graphic offer, every skill seen, none for an element that offers none; `GraphicLearner`, an observer of
  the game connections registered by the bots module, teaches it from every map's information any bot receives, by the
  elements the server lists on the map, whatever the activity. D3B's database keeps it from one run to the next.
- **The journal** (D112, D113), in `D3B.Bot.Journal`: each bot's last 2,000 entries (`BotActor.Journal`), written by the
  behaviors once an action is over, with its outcome and duration: a walk asked for itself, a map change, a use, a
  travel, banking; then the bot's connection, pause, resuming and fights, and its activity's start, interruptions,
  unloading and end. An action begun within another (`BotContext.BeginAction`) is one deeper, and a walk made for
  another action has no entry. Entries name the game's ids, never the protocol's messages. `RecentItems<T>` keeps them,
  read by the app as they come.
- **The host's problems** (D114): `HostProblems`, a logging provider, keeps the host's last 2,000 warnings and errors
  that name no bot.
- **Delays** (D151, D152): every delay goes through `TimeProvider`, and every wait of a behavior reads `BotDelays`
  (`BotContext.Delays`): the client's display time after a map and after a window, 0.5 s measured and up to 3 s; the
  server's answer, 3 s; a walk's arrival, 1 s, and each of its steps, 0.5 s; the new map, 10 s; a use's end, 2 s; a
  deposit's follow-up, 1 s; recovery, 30 s. Each is its measured value by default, the least, raised only, a fixed value
  never drawn at random. `Bots.Delays` changes them for every bot at once, and a request waiting for the map reckons its
  display time again once waited. The host reads them from its configuration in seconds (`Delays:Display`,
  `Delays:Answer`, `Delays:WalkArrival`, `Delays:WalkStep`, `Delays:Placed`, `Delays:UseEnd`, `Delays:BankFollow`,
  `Delays:Recovery`), and one below its measured value stops it, saying which. An activity's own delays stay its own,
  such as `HarvestActivity`'s.

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
  element, acting on an NPC, replying, moving a stack or kamas into the storage, leaving a dialog, teleporting, leaving
  the haven bag. The client follows the server's answers as after its own actions, confirming a walk once arrived and
  asking what is on a new map, so D3B never sends those; the bot learns that an action is over from what the server
  answers.
- Framing, measured (D38): a varint length, then a protobuf message, in the clear. `FrameReader` reads frames from a
  `SequenceReader<byte>` without copying; a benchmark in `benchmarks/D3B.Network.Benchmarks` measures it. The game
  connection's envelope carries an `Any` whose type URL is `type.ankama.com/` and the obfuscated message's name,
  measured (D56); the connection phase has none.
- The obfuscated schema, built in session 3c (D39, D40): `scripts/schema.cs` reads the installed build's protocol from
  its IL2CPP code with Cpp2IL, its structure from the metadata and the encoding of every field from the machine code,
  and writes it as a `FileDescriptorSet` beside the build's pack, `schema-1.binpb`. `scripts/capture.cs --schema` reads
  captures with it, field by field; with `--messages`, the game's frames by the messages their type URLs name, in order
  and with their time, `--mapping` naming the readable ones (D178).
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
  and writes it beside the pack, `mapping-9.binpb` (`MappingFile`, D178), only when it holds. For what is left open it
  lists the build's messages that fit, which the maintainer pins (D54). The mapping records the schema's SHA-256 and the
  readable protocol's digest (`ProtocolDigest`); `MappingFile.FormatVersion` changes with `mapping.proto` or that
  digest.
- The per-build translation, built in session 3e for the connection phase (D47): `ProtocolTranslation`, in
  `D3B.Protocol.Translation`, exists only for a build whose mapping holds the contract and pairs every readable field and
  value with its own number, since the readable classes read the wire as it is. A frame is decoded once and understood
  only when its message encodes back to its length; one that is not is passed on as it is, and never rewritten. A game
  frame's type URL names the build's message, which the mapping turns into the readable one (`DecodeGame`, D58); a frame
  of a partial message is read without being understood whole (D55).
- The version gate (D48, D172): the protocol module (`ProtocolModule`, in `D3B.Bot`) registers `ProtocolSource`, which
  finds the build's schema and mapping beside its pack on this machine, else among the app's own, and the translation,
  which the host asks for before it starts. Without a mapping that holds, the host logs what fails and the commands mapping the
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
- The relay's timeouts (D151): `RelayTimeouts`, how long it tries each address of the server, keeps a side open once the
  other closed, holds a frame not yet whole, and keeps a server selection for its game connection, 10, 5, 5 and 60 s by
  default and raised only; read once, as the relay is built, from `Network:Timeouts:Connect`, `Close`, `Hold` and
  `Selection`, in seconds, into `RelaySettings`.
- The protocol inspector's frames (D115), in `D3B.Bot.Inspection`: `FrameRecorder`, a game observer, keeps each bot's
  last 3,000 frames, both sides, and D3B's own requests, which observers see apart once sent (`IGameObserver.OnSent`).
  `FrameFields` gives a readable message's fields, the ticket hidden, or the shape of one the readable protocol lacks;
  `CaptureFrames` reads a game connection's capture of the installed build through its translation.

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
- A pack of format 5 holds (D27, D29, D139, D182):
  - every map as its own entry, its neighbours, cells and interactive elements with their graphics (`MapData`), read
    through a `PackCache` by `GameMaps`;
  - the client's world graph, a vertex per zone of a map and transitions between them (`WorldGraph`), and the atlas
    of map positions, sub-areas with their resources, areas and zaaps (`WorldAtlas`), both read once by `GameWorld`;
  - the game tables a harvesting and banking bot uses: items and their types (`ItemCatalog`), jobs and skills
    (`JobCatalog`), interactives (`InteractiveCatalog`), banks and bankers with the replies that open the bank
    (`BankCatalog`), each read once by `GameTables`; items and jobs name their icon;
  - the icons of the items a harvest names and of the professions, 64 pixels square in raw RGBA, decoded from the client's
    BC7 textures by the extraction tool (`IconCatalog`, D135 to D137), read once by `GameTables`;
  - what fights need, each read once by `GameTables`: the spells, their levels with every field and their effects, each
    with its zone, target mask and triggers as the client writes them, the spells' variants, and the Rogue's bombs and
    walls (`SpellCatalog`, in `D3B.Data.Spells`); the states (`SpellStateCatalog`); the effects, by the id spells name and
    the server announces (`EffectCatalog`); the monsters, their grades, spells by grade, characteristic ratios and races
    (`MonsterCatalog`); the breeds, their spells, roles and costs (`BreedCatalog`); the characteristics and the Lua
    formulas scaling a monster's, kept and not run (`CharacteristicCatalog`). Their flags have the bits the client's code
    gives them, in D3B's own enums (D183). A cast names its level by id, a monster fighter its monster and grade (D184);
  - in French and English, the texts these name (`GameTexts`).
- Which interactive an element of a map is, and which skills it offers, the client's data does not say: the server
  does, when a character enters the map. The data links a profession's skill to its resource, and the resource to the
  sub-areas listing it.
- Packs, schemas and mappings are assets of this repository's releases, one release per client build, tagged
  `client-<version>`, each asset with its SHA-256 beside it, never committed (D10, D11, D24, D39). `ClientRelease`
  names them; `scripts/release.cs` publishes a build's pack, schema and mapping together, once the mapping holds, never
  replacing an asset already published (D33, D50). The app's release workflow takes them from there with
  `scripts/embed.cs`, checked, to ship them beside the app (D172).
- The data module (`DataModule`, in `D3B.Bot`, D31) registers, as singletons every bot shares, the installed
  `ClientBuild`, the `PackSource` that finds its pack, the `GamePack`, opened once and closed with the host, and the
  stores reading it: `GameTexts`, `GameMaps`, `GameWorld` and `GameTables`. The configuration names the client
  (`Client:Directory`) and the packs' directory on this machine (`Data:Directory`).
- Before the host starts, it opens the pack of the installed build (D172): the one on this machine, where the tools write,
  else the one the published app ships in `data/<version>/` beside its executable (`DataSettings.Locate`). Nothing is
  downloaded. With no pack for the installed build, the host does not start: it says which build lacks one and how to
  extract it, names that build to the app (`BotHostStart.MissingBuild`), and exits with code 1. The pack of another build
  is never used.

## Desktop app

Built in session 5a (D91 to D94), in `D3B.App`, on Avalonia 12, ShadUI and CommunityToolkit.Mvvm; its pages in 5b,
5c, 5e and 5d (D96, D101, D111, D120); its settings page in 6c (D151); redesigned in 6d (D159 to D162); published and
updating itself in 6f (D171 to D173).

- **The bots run in the app's process** (D91): `HostRunner`, in `D3B.App.Hosting`, starts the host's composition with
  the app, off the UI's thread, the pack's download included, and stops it once the window is gone, which disconnects
  the clients and gives them their configuration back. The window shows the host getting ready, running for the
  installed client's build, or why it did not start.
- **The window**: dark by default; where the host stands in the title bar, nothing else (D162). Its icons are Lucide's,
  drawn by `Icon` from `Themes/Icons.axaml` (D161), its statuses by `StatusPill`, in the tone each says (`Tone`).
- **One scale** (D159, D163), held by `Themes/Styles.axaml` and the views: spacings 4, 8, 12, 16, 24 and 32, fonts 12, 14,
  16, 22 and 28 (`Caption`, the window's text, `Title`, `PageTitle`, `Figure`), every control of a toolbar or a row 36 high,
  the icon buttons of a list's items 32, radii 6, 8 and 12, ShadUI's included, icons 14, 16 and 20. A page with nothing to
  show says so with an `EmptyState`, the same on every page. Every page is an `AppPage` (`D3B.App.Controls`): its title or a header of its own, its actions on the right, the
  same margins and the window's width up to 1,200 pixels; it fades in as it shows. A setting's help, a delay's default
  and what a page is for are tooltips on a mark beside their name.
- **Dialogs and toasts** (D159, D162): `Dialogs` holds the dialog shown, a `QuestionViewModel` or a dialog's own view model
  (choosing resources, a new profile), which the window shows through ShadUI's dialog host, closed by its buttons and
  Escape; `Toasts` says what was done and what a bot met, which the window shows through ShadUI's toasts. The view models
  only open, close and say, tested without a window.
- **The sidebar**, always (D101, D111, D120, D162): ShadUI's, the bots with their count, the accounts, the profiles, the
  map, the statistics (D146), the logs and the inspector, a line once a module registered a page, then the modules' pages
  in the catalogue's order (D96), each named, folded to their icons on a click that `app.json` remembers; at its foot the
  settings page (D151), shown whether the host runs or not. A module's page (`ModulePageViewModel`) is made the first
  time it shows and kept, refreshed every 250 ms while it shows through its context (`AppPageContext`, in
  `D3B.App.Pages`); one that throws, or is no control, shows what it threw instead (`PageFailureViewModel`), logged, and
  the bots and the other pages go on. What a page's own code throws on the UI's thread, which nothing caught, is told by
  the page's assembly in its stack and fails that page alone; the app's own faults still end it. A page looks like the
  app's through the theme, Inter and ShadUI's styles it inherits, and the app's classes it may use: `PageHeader`,
  `PageTitle`, `Plate`, `PlateFooter`, `Card` and the typography's.
- **The bots page**: a card per bot, in as many columns as the width holds (`CardGrid`), each whole: who, a dot of its
  connection, said only when it is not connected, and the player's pause, resumed from the card; where, the coordinates large and the sub-area named from the
  pack's texts; pods and kamas; what it is in the middle of; its activity, started from a profile or from the catalogue
  with its defaults (D93, D99), the character's own profile chosen, stopped, with its status, steps and last
  interruption, and the error it failed with (D103). A profile whose activity is gone, such as a Lua script's, is offered
  with it said gone, and does not start (D119). An activity's report shows there: what it does, its tour as a bar and how
  long it ran, what it counts with each item's icon and rate an hour, the experience gained and the fights, its warnings, and why it
  ended (D131, D141, D144, D146). What a bot meets while the app looks, its activity done, failed or still full, the
  player taking it over, a disconnection while it ran, a fight, is a toast with a way to its journal or map (D162).
- **The accounts page** (D101): a card per account the relay saw, its subscription and when it was seen, forgotten after a
  question; its characters in rows under columns named once, each with the profile it plays and whether it starts on
  connection.
- **The profiles page** (D101, D159): the profiles as cards, each with its name, activity, settings at a glance and
  characters, edited, copied or deleted. A new profile, or a copy, is named in a dialog with its activity
  (`NewProfileViewModel`), then edited. The editor takes the page: its name in its header, beside its activity, cancel,
  save and a menu; its settings by section, a card each, a row per setting, its name and help left and its control right,
  a list or a zone the card's width (`D3B.App.ViewModels.Settings`), checked as they change, searched by name for the
  game's values, saved once they hold; beside them, the activity's preview, the zone's mini-map (`ZoneMapCanvas`, each map
  a square at its coordinates from the atlas, the probable ones marked, those left out dim) and its figures, and who plays
  the profile. Closing it with changes asks to save, discard or stay; Ctrl+S saves. A profile whose activity is gone says
  so, and is given another or deleted (D119). Resources are rows in their order, each with its icon, its profession's
  icon and level, and a mark when a character playing it lacks the level; they are chosen several at once in a dialog,
  searched and filtered by profession (`ResourcePickerViewModel`); a sub-area setting suggested by resources lists the
  sub-areas where they grow; a percentage is a bar, a short choice segments (D127, D138, D141). The professions that gather
  are chips, all of them first and without a profession last (`GatheringJobsFieldViewModel`); a list leaving resources out
  offers to exclude and warns of no level; a number says what it counts after its box; and the characters playing the
  profile each say what the settings lead to for them, such as the place chosen for their levels (`PlayerNoteViewModel`,
  D165 to D167). The pack's icons are drawn
  as bitmaps once per pack (`GameIcons`); a pack without them, or an item without one, keeps the neutral square.
- **The statistics page** (D146, `D3B.App.ViewModels.Statistics`): what the kept runs brought, by character and period,
  today, 7 days or everything, both in its header: the resources, the time, the rate an hour and the levels gained; each
  resource with its icon, rate and a bar; the runs, the latest first, grouped by who ran what, the one going on or failed
  saying so.
- **The settings page** (D151, `SettingsViewModel`, `DelayViewModel`): the machine's settings, not a profile's, a card of
  rows each. The client's display time, a bar from 0.5 to 3 s; the delays, each a box from its default to its most, its
  help and measured or D3B's value in a tooltip, a dot when raised, and a button restoring them all but the display time,
  the bots' applying at once (`Bots.Delays`), the relay's from the next start; the captures, `Network:Captures:Enabled`,
  from the next start, with whether the host records now and where; the language and the theme; where the database, the
  pack and the app's own file are, each copied on a click; D3B's version, the client build it ships and its update
  (D173). Once the host runs, the page shows the delays its
  bots wait, which its command line may have raised; changed before, the page's are given to them.
- **The logs page** (D111 to D114, `D3B.App.ViewModels.Logs`): every source by time, the host's problems, or a bot's
  journal, which its card opens, a profile that did not start on connection included (D119), chosen top right; filtered
  by tone and text, followed, copied, in one toolbar. `JournalLines` says each entry in the app's
  language, maps by their coordinates and sub-area, elements and skills by their names in the pack.
- **The protocol inspector** (D111, D115, `D3B.App.ViewModels.Inspector`): a bot's frames live, frozen on demand, or a
  capture of the game connection replayed, both chosen top right; filtered by sender, name and whether the readable
  protocol read them, in one toolbar; a frame unfolded in place, the ticket hidden. Both pages and the map look at one
  bot (`BotChoice`), each chosen in the same selector.
- **The map page** (D120 to D122, `D3B.App.ViewModels.Maps`): the bot's map drawn from the pack as its character moves,
  which its card opens. `MapCanvas`, in `D3B.App.Controls`, draws a scene flat in the theme's palette (`MapPalette`), a
  map's fixed geometries built once, in layers toggled in a menu that shows each one's marks, the legend (D162): the cells, the character and its path, the elements by
  what the server says of them, the NPCs, the exits from the world graph (`MapExits`), the other bots, the zones and
  stairs, and the fight; it says the cell under the pointer, which the page describes in the app's language (`MapNames`). The haven
  bag's and a fight's maps are drawn as the others; in a fight, the Fight layer tints the teams' placement cells red and
  blue during the placement, the server's (D180), then draws the character on its fighter's cell and its allies and
  enemies as squares, each described under the pointer with its life, AP and MP, a monster by its name (D184); a resource by its icon, faded once harvested (D141). A running
  activity's report adds a line, an Activity layer ringing the elements it wants and the one it goes for and marking the
  exit it heads to, and its tour's mini-map, gone through, probable, to see, without a resource, growing back and isolated
  (D131, D138, D145, D149).
- **Published** (D171): pushing a tag `v<version>` has `.github/workflows/release.yml` publish the app self-contained for
  win-x64, put the pack, schema and mapping of the build `ClientVersion` names in `data/<version>/` beside it with
  `scripts/embed.cs` (D172), and pack it with Velopack into the release `v<version>`: an installer, a portable zip, full and
  delta packages. Installed per user into `%LOCALAPPDATA%\D3B.App`, apart from `%LOCALAPPDATA%\D3B`, which uninstalling
  keeps. `LICENSE` and `THIRD-PARTY-NOTICES.md` ship beside it.
- **Updates** (D173, `D3B.App.Updates`): `Program` lets Velopack run first, as an install, an update or an uninstall asks;
  `AppUpdates` checks the repository's releases as the app starts (`VelopackUpdateClient`, or the folder `Updates:Source`
  names), downloads a newer version at once, and installs it when the user restarts from the window's banner, asked first
  while bots play, or once the window is gone and the host stopped. `UpdatesViewModel` says it in the banner and on the
  settings page, with the app's version and the client build it ships (`AppRelease`); when the host lacks the installed
  build's files (D172), the installed app says so instead of the host's message, with the update and a button installing
  it. Run from its code, the app does not check.
- **View models** (`D3B.App.ViewModels`) read the bots' snapshots every 250 ms, on the UI's thread (D9), the page shown
  alone: the bots' loops never wait for the app. Views are thin, and bind compiled.
- **Translation**: `Localizer`, in `D3B.App.Localization`, reads `Strings.resx` in English and `Strings.fr.resx` in
  French; the language starts as the system's, or the one the app was left in, and changes live, the views binding
  through `{l:Tr Key}`.
- **Preferences** (`AppPreferences`): the language, the theme and the sidebar folded or not, remembered in
  `%LOCALAPPDATA%\D3B\app.json`, read before the window shows (D101, D162); and the host's settings the settings page changed, by their configuration's keys and values,
  such as `Delays:Display` or `Network:Captures:Enabled`, one given back its default forgotten (D151). The app reads the
  file before the host, which starts with them (`HostRunner`, `BotHost.CreateBuilder(args, settings)`), its own
  configuration and command line winning over them.

## Storage

Built in session 5c (D99, D100), in `D3B.Storage`, which the app and `D3B.Host` add to the host's composition; the maps'
graphics in session 6b (D138); the resources' growing back and the runs in session 6c (D145, D146).

- **The database**: SQLite through EF Core, in `%LOCALAPPDATA%\D3B\d3b.db` or `Storage:Path`, its migrations applied
  before the host starts (`StoragePreparation`, an `IHostPreparation`); one that does not open stops the host, which says
  why. Migrations are written by `dotnet-ef`, a local tool, through a design-time context.
- **Accounts and characters** (`AccountBook`): recorded as the relay sees them (`AccountRecorder`, an observer of the game
  connections), the server's list and the character the client chooses, queued off the relay's loop; forgotten on
  demand. Each character may play a profile, and start it on connection.
- **Profiles** (`ProfileBook`): a name, an activity and its settings as JSON, shared by characters. The activities' presets
  are stored once as profiles as the database opens, named in the system's language, `Presets` keeping which were, so that
  one deleted never comes back (`StorePresetsAsync`, D168). `ProfileStarter`
  starts one on a bot once its activity is registered and its settings read and hold; `StartOnConnection` starts a
  character's when its bot is made, if it asks.
- **The maps' graphics** (`GraphicRecorder`, D138): what the bots learn of each graphic, its skills as JSON, read into
  `GraphicSkills` once the database opens and written as the bots learn, queued off the relays' loops, whatever the
  client's build.
- **The resources' growing back** (`RegrowthRecorder`, D145): `Regrowths` by skill, the longest seen whole, how many and the
  least bound, read back before the host starts and written as the bots learn, as the graphics are.
- **Runs** (`RunRecorder`, `RunBook`, D146): each activity's run, its character, profile, times and outcome, the items it
  brought, the experience its character gained and its banks and fights; the recorder looks at the bots every 5 s and
  writes a run as it begins, banks, once a minute while its figures change, and once over. The book reads them back after
  each write, for the app's statistics.
- **In memory**: both books are read once the database opens, then again after each write; the app reads them every
  refresh, and their version tells it they changed.
- **No secret** is kept: no Data Protection yet (D100).

## Mods

Lua scripts went (D116, D119): users extend the bot with nothing yet. Mods come in blueprint style at step 7, after step
6 (D117): graphs of nodes edited on a canvas in the app, their nodes over D3B's behaviors, a graph run as an activity
(D87). What a node is, how a graph runs and resumes, where mods live and which canvas are Q13's, settled at that step.

## Engine

Pure logic with no I/O, in `D3B.Engine`, which references `D3B.Data` for the cells and the world. Built in session 4a
(D68), 4b (D71, D73), 4c (D75, D76), 4d (D79), 4e (D83) and 4f (D86):

- `MapGeometry`, in `D3B.Engine.Maps`: 14 columns of 40 staggered rows, the eight directions between neighbours, the
  coordinates the server names a cell by (`MapPoint`), measured on the captures, the distance between two cells, the
  sum of their coordinates' differences, by which the server measures a skill's range, and whether a cell of an edge
  leaves the map in a direction, which the paths and the app's map share (D121).
- `ElementCells`: the cells a character uses an element from, as the client chooses them, the walkable neighbours of the
  element's cell in the character's zone, else the zone's walkable cells nearest it.
- `CellPathfinder`: A* over the cells a character may walk on, a step across passing beside at least one walkable cell,
  steps weighed by the time the character takes, to one cell or the nearest of several; on a map with stairs, cells of
  moveZone 1, a step changes floor by 10 at most but along a stair (D86). `CellPath` gives the turning points a request
  names. Nothing is kept between calls.
- `Criterion`, in `D3B.Engine.Criteria`: a criterion of the client's, read once into a tree and answered on three values
  from `CriterionFacts`, what the bot knows; a code it does not know is unknown.
- `WorldInsides`, in `D3B.Engine.World`: the maps a map's doors lead into at its coordinates on no world map, such as a
  bank's insides (D79).
- `WorldPathfinder`, in `D3B.Engine.World`: Dijkstra's over the world graph's vertices, a zone of a map each, through
  scrolls, scrolls with an action, map actions and doors whose criteria are met (`WorldPathRules`), each change costing
  the same, to the nearest of several maps; sub-areas closed to accounts without a subscription are entered by
  subscribed ones only. A subscribed character also teleports, from every map the server lets it teleport from, to the
  zaap of every map it knows one on whose cost it carries (`TeleportCost`), one change like the others (D83). It gives
  the cells each map is left from (`ExitCells`), a door's around its element. Built once per pack and shared; a search
  keeps its state in its own arrays, and takes under 0.7 ms (`benchmarks/D3B.Engine.Benchmarks`).
- The rules of a fight, in `D3B.Engine.Fights` (9c, D185 to D190), pure, on the pack's catalogs (D182):
  - `Combatant`, a fighter as the rules see it: team, cell, AP, MP, range, lock, dodge, life, summons, breed or monster
    and its properties, summoner, states. `CellSet`, a set of a map's cells as 560 bits, which the rules pass around
    without allocating. `FightMap`, a map's cells walkable in a fight and letting sight through, built once per map.
  - `SpellRange`: the cells a level reaches from its caster's, a diamond, a line or diagonals, its most boosted by the
    caster's range. `LineOfSight`: the cells between two cells, a corner touching only, none blocking by its data or a
    fighter, the same both ways.
  - `SpellZones`: the cells each of the 26 shapes covers, centred on the cell cast on and directed from the caster.
    `TargetMask`: an effect's mask parsed once, answering on three values whether it selects a fighter.
  - `CastRules`, built once per pack: whether a fighter may cast a level on a cell, the refusal named (`CastRefusal`)
    in the order the checks run, from its `CastHistory`; the cells it may cast on; the fighters each effect reaches.
    The level's criterion on states is a `Criterion` answering `HS` on `CriterionFacts.States`.
  - `FightMoves`, built once per pack: the four cells a fighter steps to, the share of its MP and AP a tackle leaves it,
    and the cells it walks to with what it keeps there.
  - Benchmarked on a captured fight's map: every cell a level may be cast on in 6.3 µs, a line of sight in 30 ns, an
    effect's reach in 1.5 µs, the cells a walk reaches in 1.2 µs.
- Later: the fight simulator at step 9, using an element not depending on line of sight (D75).

## Fights

Step 9, in seven sessions, each layer measured before the next (D177). Built in session 9a: the fight's protocol, named
and pinned in `actors.proto`, `fights.proto` and `fight_actions.proto` (D178); the fight's state in the bot's (D179, in
[Bot run time](#bot-run-time)); the fight on the app's map (D180). Built in session 9b: the fight's data in the pack,
format 5 (D182, in [Game data](#game-data)), its flags read in the client's code (D183), the cast's level and the
monsters checked on the captures, and the monsters named on the map (D184). Built in session 9c: the rules of a cast and
of a walk in `D3B.Engine` (D185 to D189, in [Engine](#engine)), checked on every cast and walk captured, and the casts the
server refuses left to captures to make (D190, Q16). The references' ideas for each session are D181.

- The protocol, measured: the server creates a fight's context, shows each fighter, gives both teams' placement cells and
  every fighter's disposition after each move, then starts the fight. Each turn it names the fighter playing, lets the
  character's client play, frames every action in nested sequences, and announces what each did by the effect's id: casts,
  AP and MP spent, life lost and healed, teleports, swaps, summons and deaths; it asks the client whether it is ready for
  the next turn. It ends with each fighter's outcome, experience and loot. The bot acts through the cast's, the walk's,
  the placement's, the readiness' and the turn's end's requests; the client sends the rest by itself.
- In `D3B.Engine`, from the pack's catalogs: the rules of a cast, line of sight, zones, masks and conditions, and walking
  with its tackle (9c, built); next, the effects' engine, Bubble's formulas measured one by one against the server's
  answers, and the states and cooldowns the server gives, named (9d).
- A fight is decided by simulating candidate actions and scoring them (D13): each turn the AI searches the best sequences
  of moves and casts and thinks again after each answer of the server (9e). The simulator applies spell effects read
  from the game data. It lives in `D3B.Engine`, with no I/O, so it is tested and benchmarked on its own.
- The hard cases through the effects, measured one at a time (9f), and strategies as modules and profile settings, per
  breed through the effects, never per class (9g).

## Tests

- The fight's state is tested on fights built as the captures measured them: placement, turns, every action taken, a
  summon, the end, and what the server says of a fighter it never showed (D179).
- Logic code in `D3B.Engine` and the state handlers is tested directly, with property tests on CsCheck for cell and
  world paths (D68, D71) and lines of sight, the same both ways and nearing their end at each cell. A fight's rules are
  tested on maps and fighters the tests build around a map's middle, with a catalog of six spells, each refusal named.
- Recorded sessions replay through an in-memory `Pipe` (`CaptureReplay`, D37); tests build the sessions they replay.
  Decoded frames are compared with the messages a test built (D49); bot states with snapshots the test holds, serialized
  as indented JSON (D70).
- Bots are tested through game connections driven as the relay shows them, on a pack of five maps, the world between
  them, a door leading into two insides, a tree and a bank, with the bots' loop waited for and the clock moved (D67);
  activities through one whose steps wait for the server, interrupted by the player, a closed connection and a fight
  (D87).
- Replays with injection (D63): a game session a test built is replayed through the relay, read by a game session whose
  observer sends a request; the test checks what each side got, what the observers saw, and the relay's capture.
- The app's view models are tested against bots played as the bots' tests play them, and its window drawn without a
  screen by Avalonia.Headless with Skia, its texts read in both languages (D94); its accounts and profiles pages against a
  database of the test's own and the game's values of a small world, named by a pack's texts in both languages (D101).
- Activities' settings are tested in `D3B.Bot.Tests`; the storage in `D3B.Storage.Tests`, on a database of the test's own,
  its recording and starting on connection through game connections played as the bots' tests play them (D99, D100).
- The app's module pages are drawn with a page of the tests' own (D109). A profile naming a Lua script, in a host with no
  activity, is shown gone on the profiles page and a bot's card, and one that does not start on connection is said in
  the bot's journal (D119).
- A bot's journal is read after the behaviors the bots' tests play, each entry and its depth (D112); the frame recorder
  through game sessions, D3B's requests included, and the inspector's fields and replays on captures the tests write
  (D115); the logs and inspector pages against bots played the same way, in both languages (D111).
- The app's dialogs and toasts are read from its view models (`Dialogs`, `Toasts`): a profile made, copied, closed with
  changes and deleted, an account forgotten, resources chosen several at once, what a bot met said once; every icon is
  parsed within its grid (D161); every page and the profile editor's dialogs are drawn without a screen in both languages
  and themes, and looked at (D159, D162).
- The map page against bots played the same way: its scene, exits, elements, NPCs, other bots, a fight's placement and
  fighters, a fight's and a missing map, and what a cell holds, in both languages; its drawing without a screen, a pixel of each mark checked in each
  theme's palette and the cell under the pointer described; its layout's cells found back from their centres (D120 to
  D122).
- Time is controlled through `FakeTimeProvider`.
