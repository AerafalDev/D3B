# Decisions

Product and architecture decisions, oldest first. Each entry states what was decided, why, and what was rejected. An
entry is never rewritten: a later entry supersedes it, and the old one is marked as superseded. Questions still open
are listed at the end, with the [roadmap](roadmap.md) step that settles them.

Decisions D1 to D18 were taken on 2026-10-27, one by one, after studying the references in `vendor/` and the bots
sold for Dofus 3. From D19 on, each entry names the roadmap step that took it; an entry the owner chose says so, the
others are local choices of the lead session.

## Brief

Set by the project owner, not up for decision:

- An open-source bot for Dofus 3 (Unity), in .NET 10.
- Better than the existing bots, open source or sold: inspired by them, never copied.
- Every feature is a module, assembled as in Bevy, in the repository (D116, the owner's change: the brief's external
  .NET plugins, then the Lua scripts of D102 that replaced them, go).
- Mods are made in blueprint style, graphs of nodes edited in the app, after the first milestone (D117, the owner's
  change).
- The desktop UI is Avalonia with ShadUI.
- The network goes through Kestrel's socket layer.
- Game data is extracted from the official client by file-based apps in `scripts/`, as in Dofus Remastered.
- Fights take their ideas from Bubble emu and BubbleBot, and do everything better.
- Every new large task starts in a new session, closed with a prompt for the next one.

## D1: .NET 10 on Windows

*2026-10-27*

D3B is written in C# on .NET 10 and runs on Windows only.

- **Why.** One runtime for the bot, its tools and its UI. The Unity readers of Dofus Remastered (MIT, same owner) and
  the Cpp2IL and AssetsTools.NET libraries run in process, where BillBot shells out to C# tools from Python. Source
  generators cover the protocol and data code. Async I/O carries many accounts in one process. The official client and
  its launcher are Windows programs.
- **Rejected.**
  - Python, continuing BillBot: its core depends on PyQt6 (11 files of `src/core`), it runs one thread per timer, and
    it has no license.
  - A Linux headless host: no need yet. The host stays free of Windows UI code, so it remains possible.

## D2: What D3B never contains

*2026-10-27*

D3B never contains, in its code, its default modules or its documentation:

- evasion of Ankama's detection: simulated mistakes, timings shaped to avoid bans, forged hardware identifiers;
- mass account creation;
- subscription or payment automation;
- disabling or bypassing Ankama Shield;
- one proxy per account to hide multi-accounting.

- **Why.** These parts exist to defeat Ankama's protections or to run farms at scale, not to play. The references have
  them (BillBot's `fake_bad_*` behaviors and `human_timings`, BubbleBot's `HardwareService`, `AccountCreation*`,
  `Subscribe*`, `ShieldDisable`, `ProxyAssociate`): they are not studied and not reproduced.
- Using any bot on the official servers breaches Ankama's terms of service and can get the account banned. D3B says
  so plainly and does not hide it.

## D3: The references in `vendor/` give ideas, never code

*2026-10-27*

`vendor/` is ignored by Git. What each reference is for:

| Reference | License | Use |
| --- | --- | --- |
| `BillBot-master` (Python) | None | Ideas: clean protobuf classes behind a translation layer, the mapping toolkit, the version gate, immutable state snapshots, the debug recorder. Code the owner wrote may be ported. |
| `Bubble.D3.Bot-main` (.NET 9) | GPL-3.0 | Ideas: datacenter reading from the installed client, generated readers, handlers by domain, fight AI by simulation, treasure hunt clue solving. |
| `bubble-emu-v2-develop` (.NET 8) | GPL-3.0 | Ideas for fights. It is a Dofus 2 emulator: its formulas are checked against Dofus 3 before use. |
| `JondoEmu-main` (.NET 10) | None | Ideas: protocol rebuilt from the Cpp2IL dump, anchors named from real captures, structural matching across builds. |
| `dofus-unity-proto-main` | None | Reference for message shapes. |
| `Dofus` (Dofus Remastered) | MIT, same owner | Unity readers copied into `scripts/` (D10), conventions, the Bevy-like plugin model. |
| `metiers` | The owner's | Lua scripts the owner wrote for Snowbot, levelling each profession from 1 to 200. They are the acceptance test of the Lua API (D12), measured in D104. |
| `moonbot` | None | The documentation of MoonBot's Lua API, of Snowbot's family: ideas for the scripts' API, added in D104. |

- **Why.** Copying GPL code would make D3B GPL (D4). Code with no license cannot be reused at all. BubbleBot's damage
  calculator is a port of Ankama's own client code. Reading for ideas and rewriting keeps D3B clean and lets it fix
  their faults: BubbleBot's shared mutable pathfinder and data races, BillBot's silently dropped fields.

## D4: MIT license, private repository until the first milestone

*2026-10-27*

D3B is published under the MIT license, as Dofus Remastered and Palforge. The repository stays private until the first
milestone (D16).

- **Rejected.**
  - GPL-3.0: it would allow BubbleBot's code, which D3 already rules out.

## D5: Modules assembled as in Bevy, for composition only

*2026-10-27*

A bot application is assembled from modules. A module implements `Build(BotBuilder)` and registers what it brings:
services, message handlers, activities, fight strategies, Lua API namespaces, settings and UI pages. A module may
require other modules; adding a module type twice builds it once. A default group gathers the standard modules
(network, protocol, data, world, movement, harvesting, bank). A plugin is an assembly that contains modules (D6).

The model applies to composition only. At run time each bot is an async actor (D9), not a world of systems run in
phases. `BotBuilder` sits on the .NET Generic Host: the host is a long-running service whose lifetime, configuration,
logging and dependency injection the Generic Host already covers.

- **Rejected.**
  - A Bevy-like run time, with resources, events and systems per bot: a bot reacts to messages and timers, not to
    frames, and async workflows say that more directly.
  - Reusing the core of `Dofus.Framework`: it would first have to be split from SDL in Dofus Remastered, and the two
    projects would be tied together.

## D6: External plugins load once, never unload

*2026-10-27. Superseded by D102: Lua scripts replace the external plugins.*

An external plugin is a folder under `plugins/`. Each loads into its own `AssemblyLoadContext`, which shares the host's
assemblies, as Palforge's `PluginLoadContext`, but is not collectible. A plugin declares its identity with an
attribute. A `dotnet new` template creates one.

- **Why.** Unloading constrains every type a plugin touches and is rarely needed: updating a plugin restarts the host.
- **Rejected.**
  - Collectible contexts, as in Palforge.
  - Modules compiled into the application only: nobody could add one from outside.
  - NuGet packages or a catalogue in the UI: more work than a folder needs at this stage.

## D7: A headless host runs the bots, the desktop app is its client

*2026-10-27. Superseded by D91: the bots run in the app's process, and `D3B.Host` runs them without the app.*

The bots run in a headless host. The Avalonia app connects to it over gRPC on a named pipe, served by Kestrel.

- **Why.** A crash or a restart of the UI does not stop the bots. A remote front stays possible later. Kestrel is
  already in the stack (D8).
- The transport, gRPC on a named pipe, is a local and reversible choice.
- **Rejected.**
  - Everything in one process: simpler at first, but the UI's faults would take the bots down.

## D8: The network goes through Kestrel's socket layer, socket mode first

*2026-10-27. Superseded in part by D23: there is no socket mode, only MITM.*

Outgoing connections use `SocketConnectionContextFactory.Create(Socket)`, Kestrel's public API since .NET 7, on a
socket connected by the bot. The MITM mode listens with Kestrel and a connection handler. Both give a
`ConnectionContext`, and the codec only sees its `IDuplexPipe`. Socket mode, with no game client, comes first. The MITM
mode comes later, mainly to capture sessions and debug.

- **Why.** Kestrel's pipes, memory pool and scheduling, with no copy of its internals. One abstraction for both modes.
  An in-memory `Pipe` replaces the network in tests and replays.
- The network project references the `Microsoft.AspNetCore.App` framework. No other project needs it.
- **Rejected.**
  - A copy of Kestrel's internal socket classes, as in BubbleBot (`Bubble.Core/Network/Transport/Sockets`).
  - `NetworkStream` with pipes on top: workable, but no better than what Kestrel already exposes.

## D9: Each bot is an async actor

*2026-10-27*

Each bot owns one inbox (`Channel<T>`) and processes it on one logical thread, so its state needs no locks. Every
delay goes through `TimeProvider`, so tests control time. Behaviors are async methods taking a `CancellationToken`:
an interruption, such as a fight starting, cancels the current behavior, and a recovery step runs before it resumes.
Logic code reads immutable snapshots of the state, an idea from BillBot.

- **Rejected.**
  - A thread per timer with callbacks, as in BillBot.
  - Mutable flags and an if-chain, as in BubbleBot (about 70 flags in `GameRuntimeState`).
  - Singletons shared between bots: BubbleBot's world pathfinder keeps the search in fields with no lock.

## D10: Game data comes from file-based apps, and ships as packs

*2026-10-27*

Data is extracted from the official client by file-based apps in `scripts/`, with Dofus Remastered's conventions:
they have no tests, and shared code is pulled in through `#:include`. The Unity readers of Dofus Remastered (on
AssetsTools.NET) are copied there, then evolve on their own. The output is packs: MemoryPack serialization,
compressed with zstd. Packs are published as release artifacts, never committed. The scripts stay usable by anyone who
prefers to extract from their own client.

- **Why.** One extraction per client build, by the maintainer, instead of one per user.
- **Risk, accepted by the owner.** Distributing data derived from the client is a legal risk.
- **Rejected.**
  - Each user extracting from their own client only: safer, but every user needs the tools and runs them at every
    update.
  - A library shared with Dofus Remastered: it would tie the release cycles of both projects.
  - JSON in Git LFS (BillBot) or a committed `maps.json` (BubbleBot).

## D11: Bot code sees clean protobuf classes, translated per build by our own pipeline

*2026-10-27*

Messages are C# classes generated from readable `.proto` files, with Google.Protobuf and Grpc.Tools. A mapping per
client build translates the obfuscated wire into these classes and back. A field that is not mapped fails loudly. The
host refuses to start when its mapping does not match the installed client's build.

The mapping comes from our own pipeline: a Cpp2IL dump of the client, the IDA Pro tracer (the owner has IDA), scored
matching, pinned pairs that are versioned, and a contract audit that fails the build. The readable `.proto` files and
the pinned pairs are committed. The mapping of each build ships with that build's data pack (D10). The pipeline takes
its ideas from BillBot (scored matching, pins, audit, version gate, update watcher) and from JondoEmu (anchors from
real captures, structural matching across builds).

- **Rejected.**
  - protobuf-net classes with obfuscated field names in the bot code, as in BubbleBot.
  - Starting from `dofus-unity-proto`: no license.
  - BillBot's translator, which drops unmapped fields silently, and its pins, which each update wipes.

## D12: Lua for routes and routines, .NET plugins for features

*2026-10-27. Superseded by D90: routes and routines are .NET plugins too, and there is no Lua.*

Users write routes and routines in Lua. The API is in the family of the Dofus Retro bots and Snowbot: namespaces
called with `:` (`global`, `map`, `inventory`, `npc`, `job`, `exchange`, `character`), and a script defines `move()`
and `bank()`, which the bot calls. Every commercial Dofus 3 bot studied offers this. The owner's profession scripts
(`vendor/metiers`) must run once adapted to it: that is the API's acceptance test, at the end of the roadmap.
Features are .NET plugins (D6). The Lua engine is LuaCSharp: pure C#, with async calls, so `move()` awaits without
blocking. A prototype confirms it first (Q1).

- **Rejected.**
  - Plugins only: users would have to code and compile.
  - Profiles in the UI only: too limited.
  - MoonSharp, no longer maintained, and NLua, native and without async calls.

## D13: Fights run on a simulator of our own

*2026-10-27*

Fights are decided by simulating candidate actions and scoring them. The idea comes from BubbleBot, which scores cells
per spell. The simulator is written from the spell effects in the game data, not ported from Ankama's code or from GPL
code. Bubble emu informs the mechanics: its formulas are Dofus 2, so each is checked against Dofus 3 (Q5). Fight
strategies, per class or per build, are modules.

- **Rejected.**
  - A greedy choice of the best-weighted action, as in BillBot, which detects life steal through the French word
    "vol" in the effect text.

## D14: Avalonia and ShadUI, in French and English

*2026-10-27*

The desktop app uses Avalonia 12, ShadUI and CommunityToolkit.Mvvm. Views stay thin, so ShadUI, still at 0.x, can
change without rewriting them. Translation is built in from the start, in French and English. The pages of the first
version: accounts and profiles, the live map with the bot's state, and logs with a protocol inspector.

- **Rejected.**
  - A web dashboard: it would allow control from a phone, but the owner chose a desktop app. The host (D7) keeps a web
    front possible.

## D15: The rest of the stack

*2026-10-27. Superseded in part by D23: D3B does not call Haapi, so the "HTTP (Haapi authentication)" row no longer
applies. The "Secrets" row waits for a secret to keep (D100). The "Scripts" row holds since D103.*

Versions are the latest stable on NuGet on 2026-10-27, unless the line says otherwise.

| Area | Choice | Why |
| --- | --- | --- |
| Host | Microsoft.Extensions.Hosting | Lifetime, configuration, logging and dependency injection for a long-running service. |
| HTTP (Haapi authentication) | `IHttpClientFactory`, Microsoft.Extensions.Http.Resilience 10.11.0 | Standard retries and timeouts. |
| Protobuf | Google.Protobuf 3.36.2, Grpc.Tools 2.85.0 | Generated at build from `.proto` files; the descriptors give the per-build translation its tables. |
| IL2CPP dump | Samboy063.Cpp2IL.Core 2022.1.0-pre-release.21 | In process. The prerelease JondoEmu uses: the last stable release dates from 2022. |
| Unity data | AssetsTools.NET 3.0.5 | The version Dofus Remastered uses. |
| Data packs | MemoryPack 1.21.4, ZstdSharp.Port 0.8.8 | Generated binary serialization; ZstdSharp is already in Dofus Remastered. |
| Pathfinding | `PriorityQueue`, our own code | No library needed. |
| Scripts | LuaCSharp 0.5.8 | D12. |
| Storage | Microsoft.EntityFrameworkCore.Sqlite 10.0.14 | Accounts, profiles and statistics, with migrations. |
| Secrets | ASP.NET Core Data Protection | Already in the framework the network references; DPAPI on Windows. |
| Logs | Serilog.Extensions.Hosting 10.0.0, Serilog.Sinks.File 7.0.0 | Structured, with the bot's id as a property; an in-memory sink feeds the UI. |
| UI | Avalonia 12.1.4, ShadUI 0.2.4, CommunityToolkit.Mvvm 8.4.2 | D14. |
| Map view | An Avalonia control drawing with `DrawingContext` | Enough for cells, entities and paths. |
| Notifications | Discord webhooks through `HttpClient` | One POST, no library. |
| Tests | xunit.v3 4.0.2, CsCheck 5.0.0, Verify.XunitV3 33.3.4, Microsoft.Extensions.TimeProvider.Testing 10.11.0 | As in Dofus Remastered, with property tests for pathfinding and line of sight, and snapshots of replayed sessions. |
| Benchmarks | BenchmarkDotNet 0.15.8 | Codec and pathfinding. |
| Build | Central package management, `Directory.Build.props`, warnings as errors, MinVer 8.0.0, GitHub Actions on Windows | Dofus Remastered's conventions. |
| Distribution | Velopack 1.2.161 | Installer and updates, later. |

No Native AOT: EF Core and the Lua engine do not suit it, and a bot does not need it.

## D16: The first milestone is harvesting and banking

*2026-10-27. Superseded in part by D23: the account plays through its official client and D3B's MITM, not in socket
mode.*

The first milestone: one account, in socket mode, harvests and banks its resources, controlled from the desktop app's
first pages (D14). The repository may go public once it is reached (D4).

- **Rejected.**
  - Fights first: they need the simulator (D13), a larger step.
  - Treasure hunts first: they need clue data and more of the world.

## D17: One large task per session, documents in Markdown

*2026-10-27*

The working agreement of Dofus Remastered applies, as written in `CLAUDE.md`: one roadmap step per session, one branch
and one pull request per session, a task is done with its tests, and each session closes with a report and a prompt
for the next one. Documents are Markdown files in `docs/`.

- **Rejected.**
  - A documentation site, as Palforge's: premature before there is anything to document for users.

## D18: The project is named D3B

*2026-10-27*

The project, its solution and its namespaces are named D3B (`D3B.*`).

## D19: The solution's layout and build

*2026-10-27, step 1. Superseded in part by D109: `samples/` holds example scripts, and no plugin.*

The projects of the [architecture](architecture.md#projects) are confirmed, in `D3B.slnx`. They target `net10.0`, not
`net10.0-windows`: nothing needs a Windows API yet, and D1 keeps a Linux host possible. The build follows Dofus
Remastered: SDK pinned in `global.json`, central package management with transitive pinning, MinVer on every project,
output under `artifacts/`, warnings as errors, `AnalysisMode` Recommended, code style enforced in the build, XML
documentation on public APIs.

- Projects with no code yet are empty class libraries. `D3B.App` stays one until step 5 makes it the Avalonia app.
  Project references are added by the step that needs them; for now only `D3B.Bot` and `D3B.Host` reference
  `D3B.Core`.
- `DefaultModules` lives in `D3B.Bot`, which will reference the projects holding the standard modules. It is empty
  until those modules exist.
- `samples/` holds example plugins, built with the solution and loaded by the tests.
- `.editorconfig` makes the C# style of `CLAUDE.md` fail the build: block bodies (IDE0021 to IDE0024, IDE0061) and
  test names without underscores (CA1707). The member order and the ban on primary constructors have no analyzer and
  stay a review rule.
- Added to D15: Microsoft.Extensions.Hosting 10.0.14 for `D3B.Core`, and Microsoft.Extensions.Diagnostics.Testing
  10.11.0, whose fake logger lets a test see what a plugin logs.
- **Rejected.**
  - `net10.0-windows` everywhere, as Palforge: it would tie the host to Windows for nothing.
  - An empty Avalonia app now: its packages and entry point belong to step 5.

## D20: How modules compose

*2026-10-27, step 1*

`BotBuilder` wraps the Generic Host's `HostApplicationBuilder` and is itself an `IHostApplicationBuilder`, so modules
register into the host's services, configuration and logging, and the host's extension methods apply. A module is
built as soon as it is added, as in Bevy and Dofus Remastered's `AppBuilder`:

- A module requires another by calling `bot.AddModule<T>()` at the start of its `Build`. The required module is then
  built first. `BotBuilder.Modules` lists modules in the order they finished building, so each comes after the modules
  it requires.
- Adding a module type twice builds it once. A module counts as added before its `Build` runs, so a requirement that
  leads back to a module being built does nothing, instead of failing: a module may add companions that require it.
- A group (`IModuleGroup`) lists modules in order. Whoever adds it may disable some of them first; disabling a module
  the group does not hold fails. A disabled module is still built when another module requires it.
- Nothing is registered after `Build`.
- **Rejected.**
  - Declared requirements (`[Requires(typeof(T))]`) with a topological sort before building: order independence and a
    readable graph, but more code and a departure from D5's model, for no need yet.
  - Failing on a requirement cycle: it would reject a module adding companions that require it.
  - Replacing a default module by another type: no plugin needs it yet. When one does, the replacement should claim
    the replaced module's type, so that requirements on it are satisfied.

## D21: The plugin contract

*2026-10-27, step 1. Superseded by D102 for external plugins.*

`bot.AddPlugins(directory)` loads the plugins under a directory. The host passes `plugins/` next to its executable,
or the `Plugins:Directory` setting, resolved from the executable's directory.

- A plugin is a folder holding an assembly named after the folder, with its dependencies and `.deps.json`. Folders
  load in the order of their names.
- The assembly holds exactly one public, concrete module class with a public parameterless constructor, marked
  `[Plugin("id")]`, with an optional name and author. That root module is added to the builder and adds the rest of
  the plugin. The plugin's version is its assembly's informational version.
- Each plugin loads once into its own `AssemblyLoadContext`, not collectible, which asks the host's context first and
  the plugin's `.deps.json` second: a plugin and the host share one D3B.Core and one framework.
- A `PluginInfo` per plugin is registered in the services, for the host's log and later the app.
- Any invalid folder, or two plugins with the same id, throws `PluginLoadException` and the host does not start. A bot
  missing a feature it was given could misbehave; a clear error at start is cheaper.
- **Rejected.**
  - An assembly-level identity attribute, or adding every module of the assembly: the root module says what the plugin
    brings, in code.
  - Skipping an invalid plugin with a warning, as Palforge does.

## D22: Plugins compile against the D3B.Core package

*2026-10-27, step 1, chosen by the owner. Superseded in part by D95: `D3B.Bot` and the libraries it names are packed
beside `D3B.Core`, and a plugin references `D3B.Bot`.*

`D3B.Core` is the only packable project, versioned by MinVer. A plugin references the `D3B.Core` package with
`ExcludeAssets="runtime"`: it compiles against it, and the host provides it at run time, so a plugin's folder holds
only its own files. The `dotnet new d3b-plugin` template in `templates/d3b-plugin` does this, takes the version to
compile against as a required parameter, and can deploy every build to a host's plugins folder.

- Until the repository goes public (D4), the package is packed into a local folder. Publishing it, and the template as
  a package, comes with the first milestone.
- CI packs D3B.Core, installs the template, creates a plugin from it and builds it against the packed package.
- **Rejected.**
  - Referencing `D3B.Core.dll` from an installed host: no feed to run, but compiling a plugin, in CI too, would need
    an installed host, and nothing would state which version a plugin targets.

## D23: MITM only, no socket mode

*2026-10-27, during step 1, chosen by the owner*

D3B never connects to Ankama's servers on its own. Every bot plays through an official client: D3B sits between the
client and the game server, with a Kestrel listener and a connection handler on the client's side, its own
connection to the server on the other, and the same codec over `IDuplexPipe` on both. This supersedes D8's socket mode
and the "in socket mode" of D16. The rest of D8 stands: Kestrel's transport, `ConnectionContext`, and an in-memory
`Pipe` in tests and replays. Roadmap step 8, the MITM mode, merges into step 3.

- **Consequences.**
  - The official client logs in, through Haapi and the login handshake. D3B handles neither, which settles Q3, and
    the Haapi row of D15 no longer applies.
  - D3B never imitates the client, so nothing it does has to get past the client's integrity checks (D2).
  - Every account needs its own running client. The memory and CPU of one client per account bound how many accounts
    a machine runs (step 10).
  - The bot acts through the client's connection, and the client must stay consistent with what the bot does: Q9.
  - How each client's connection reaches D3B is Q8.
- **Rejected.**
  - Socket mode first, MITM later (D8): a bot without a client is lighter, but it must reimplement the login, which
    may touch the client's integrity checks (Q3).

## D24: Packs ship as releases of the D3B repository, one release per client build

*2026-10-27, step 2, chosen by the owner. Settles Q4.*

Data packs, and from step 3 the protocol mappings (D11), are assets of the D3B repository's GitHub releases.

- **Identity.** A client build is named by the `Version` line of `Dofus_Data/StreamingAssets/version`, such as
  `3.7.12.12`: every build measured so far has its own. `ClientBuild`, in `D3B.Data`, reads it and accepts only numbers
  separated by dots, since the version ends up in paths and URLs.
- **Releases.** One release per client build, tagged `client-<version>`. Its assets: the pack `data-<format>.pack`, the
  protocol mapping from step 3, and their SHA-256. The pack format is in the file name, so that a D3B reading a newer
  format adds its pack to the same release while older hosts keep theirs. MinVer only reads tags starting with `v`, so
  these tags leave D3B's versions alone.
- **Fingerprint.** A pack records the SHA-256 of the client files it was read from, through their SHA1 in the
  launcher's `manifest.json`. A published pack is never replaced by one with another fingerprint under the same
  version: should Ankama change content without changing the version, the difference shows, and the identity then
  grows to include the fingerprint.
- **Fetching**, built in session 2d. The host reads the installed client's version and looks for the pack under
  `%LOCALAPPDATA%\D3B\data\<version>\`, where the extraction tool writes by default, so a pack extracted locally is
  used first and works offline. Otherwise it downloads the release asset, checks its SHA-256, then moves it into place.
  With no pack for the installed build, the host does not start, and says which build is missing and how to extract
  it. While the repository is private (D4), a download needs a GitHub token; only the owner runs D3B until then.
- **Publishing.** The maintainer extracts with `scripts/extract.cs` and creates the release. CI cannot: it has no
  client.
- **Risk, accepted by the owner.** Ankama has filed DMCA notices on GitHub before (2018, against forks of TouchEmu). A
  notice against the packs would target this repository, its code included. The scripts keep working to extract
  locally, whatever happens to the releases.
- **Rejected.**
  - A separate repository for the packs, which the lead session recommended: a notice would reach only the data. The
    owner prefers one repository.
  - Object storage (S3, R2): an account, keys and costs to manage, for the same risk elsewhere.
  - Falling back to the pack of another build: the mapping must match its build exactly (D11), and data that differs
    could make a bot misbehave.

## D25: The pack format

*2026-10-27, step 2. Superseded in part by D28: entries are compressed several at once, still deterministically, and
there is no zstd dictionary.*

A pack is one file, written by `PackWriter` and read by `GamePack`, both in `D3B.Data`: the extraction tool references
`D3B.Data` through `#:project`, so the format has one definition.

- **Layout.** A header (the magic `D3B-PACK` and the format version), the entries, an index, then a trailer (the
  index's offset and lengths, and the magic again). The index comes last, so the writer streams entries; a pack whose
  writing was cut short has no trailer and is rejected. The format version comes first, so a pack of another format is
  reported as such rather than as damaged.
- **Entries.** Each has a name, such as `texts/fr`, and is one MemoryPack payload compressed as its own zstd frame with
  zstd's checksum: a damaged entry fails when it is read, alone. The index records the type each entry was written as,
  and a read must ask for that type. MemoryPack's default layout is not version tolerant: `GamePack.FormatVersion`
  changes whenever an entry type does, and a host reads only its own format.
- **Lazy reading.** Opening a pack reads its index only: about 60 ms for the 11 MB pack of session 2a. `GamePack.Read`
  reads one entry with positional reads, safe from any number of threads, and keeps nothing. `PackCache` keeps the
  entries last read within a budget in bytes, weighed by their decompressed length, and drops the least recently used:
  maps go through it from session 2b. A whole table, such as a language's texts, is read once and kept by its store.
- **Determinism.** zstd on one thread at level 19, and MemoryPack in declaration order: one client and one extractor
  give the same bytes, checked on client 3.7.12.12. A pack can be compared with another by its hash.
- **Texts.** One `TextTable` per language, French and English: identifiers sorted in one array and found by binary
  search, texts in another. For now every numbered text, 339,912 per language and 11.4 MB for both; session 2c keeps
  only those the extracted tables name. The keyed texts, the client's interface, are left out.
- **Rejected.**
  - A directory of files per pack: more downloads and hashes, where one file is one release asset and one SHA-256.
  - MemoryPack's version-tolerant mode: packs are per build and per format, so tolerance would only slow reading.
  - SQLite: queries D3B does not need, for a format that is read whole or by key.
  - A zstd dictionary trained on the entries: it may pay for many small maps; session 2b measures it.

## D26: The data tools

*2026-10-27, step 2*

- `scripts/unity/` holds what several tools share, copied from Dofus Remastered and now evolving on their own:
  `ClientInstall`, `UnityBundle`, `I18nFile` and `LauncherManifest`, which now hashes the files its manifest lacks,
  since a fingerprint must follow content. Dofus Remastered's typed readers (`UnityReader`, `UnityTypeTree`,
  `UnityTypeNode`, `UnityFields`) come with the map reader in session 2b, their first user, so that no copied file sits
  unbuilt.
- `scripts/inspect.cs` is Dofus Remastered's inspection tool without its texture and shader commands, which a bot does
  not need: `summary`, `bundles`, `types`, `objects`, `dump`, `layout` and `texts`.
- `scripts/extract.cs` writes the pack of the installed client's build to `<output>/<version>/data-<format>.pack`, by
  default where the host looks first (D24). It does nothing when the pack there has the same fingerprint and the same
  extractor version, and writes beside the pack then moves it, so a pack is never half written under its name.
- CI builds both tools, as Dofus Remastered's does, so that they keep compiling.
- Added to D15: System.CommandLine 2.0.14, the latest stable, for the tools' command lines.
- `docs/client.md` records what the client holds that D3B uses: layouts and counts, measured, never content.

## D27: Maps and the world in the pack, format 2

*2026-10-27, step 2, session 2b*

A pack now holds, besides the texts, every map, the client's world graph, and an atlas of map positions, sub-areas,
areas and zaaps, measured in [client.md](client.md). `GamePack.FormatVersion` is 2.

- **Maps.** One entry per map, `maps/<id>`: a `MapData` with its four neighbours, its 560 cells and its interactive
  elements, each an identifier and a cell. Both of the client's layouts are read, `ClientMapData` and `MapMetadata`,
  17,722 maps; graphics, materials, effects and sounds are left out. `GameMaps` reads a map when first asked for, through
  a `PackCache` of 16 MB by default.
- **Cells** are a packed unmanaged struct of 9 bytes, `MapCell`: the client's booleans as `MapCellProperties`, and its
  numbers sized after the ranges measured. MemoryPack copies an array of them as raw bytes, so a map is about 5 KB and
  reads in microseconds. The constructor rejects a value that does not fit, so a client widening a field fails the
  extraction instead of being misread. Fields whose meaning is not measured, such as `moveZone` and `arrow`, are kept
  as they are.
- **Zones.** A cell's zone is the high four bits of its `linkedZone` (`MapCell.Zone`), the hypothesis of BillBot and
  BubbleBot, confirmed: every vertex's zone is that of walkable cells of its map, and on 10,795 of the 10,981 maps the
  two sets of zones are equal; on the others, cells hold zones with no way out, which the graph leaves out.
- **Interactive elements** are keyed by map and identifier: an identifier is unique within a map only. An element's
  cell is its own, or that of the middleground group listing it; the few drawn without a cell keep
  `MapInteractive.NoCell`.
- **World graph.** One entry, `world/graph`, read once by `GameWorld`: vertices sorted by map and zone, edges by source
  and destination, transitions contiguous per edge, criteria in one table, all packed unmanaged structs except the
  criteria. Transition types are the four measured, `Scroll`, `ScrollAction`, `MapAction` and `Interactive`; the
  extractor fails on another type, or on a skill or element where one was never measured, rather than guess. Criteria
  stay text until step 4 parses them.
- **Atlas.** One entry, `world/atlas`, read once: positions, sub-areas (what travel needs: area, level, associated zaap,
  access without a subscription), areas and zaaps, each table sorted by identifier. `m_flags` is kept raw: its bits are
  not measured.
- **Checks.** The extractor reads and checks everything before it writes: every vertex's zone on a walkable cell of
  its map, every interactive transition's element on its map, every position's map held, every zaap on the graph.
- **Format test.** `PackFormatTests` hashes what defines the format: the header and trailer, and, for every entry type,
  its name and the bytes MemoryPack writes for a sample whose every field has its own value. It fails when that hash is
  not the one recorded for `GamePack.FormatVersion`; a format's line is never edited once a pack of it is published.
- `scripts/unity/` gains Dofus Remastered's typed readers, as D26 planned, with what maps and tables need: 8, 16 and
  64-bit integers, integer arrays, counts checked against the bytes left, fields named by their property when the client
  serializes a backing field (`<interactiveId>k__BackingField`), a shared reader of managed reference registries, and
  `UnityDataTable` for `DataRoot` tables.
- **Rejected.**
  - One entry for all maps: a host would read 90 MB to use a few maps.
  - Cells as a struct of arrays: 1.4% smaller compressed, for a clumsier API.
  - Transitions as classes holding their criterion: 31,329 objects to build, where a struct array is copied.
  - Leaving out the `MapMetadata` maps, on no map of the graph: they cost 1,739 small entries, and a bot may still
    stand on one.
  - Keeping `MapScrollActionsDataRoot` and the arrow cell lists: the graph's scroll actions already lead where the
    server sends, and nothing reads the lists.

## D28: Packs compressed at zstd 19, several entries at once, without a dictionary

*2026-10-27, step 2, session 2b; the dictionary chosen by the owner. Supersedes in part D25.*

Measured on the pack of 3.7.12.12 (client.md):

| zstd level | Entries compressed | One thread | 16 threads |
| ---: | ---: | ---: | ---: |
| 15 | 17.7 MB | 23.8 s | 6.2 s |
| 19 | 15.5 MB | 45.4 s | 10.3 s |
| 22 | 15.2 MB | 56.4 s | 14.5 s |

- **Level.** 19 stays the default: 22 saves 2% for a quarter more time, 15 costs 14%.
- **Parallel compression.** `PackWriter` serializes each entry as it is added, compresses several at once on the thread
  pool, each as its own frame on one thread, and writes them in the order they were added. The bytes do not change: the
  parallel pack equals the sequential one but for its format number, and two extractions give the same SHA-256.
  Extraction takes about 25 s instead of 68 s.
- **No dictionary.** A 64 KiB zstd dictionary trained on the maps shrinks them from 3.72 MB to 3.29 MB with the
  dictionary, 2.8% of the pack, and decompression of every map from 62 to 34 ms. Not worth a dictionary entry, a field
  per entry and a training kept deterministic. The texts are three quarters of the pack; once session 2c keeps only the
  texts tables name, the maps weigh more, and the question can be measured again.
- **Rejected.**
  - zstd's own multithreading inside a frame: the gain is on the two text entries only, and its output would need its
    own proof of determinism.

## D29: Game tables in the pack, format 3

*2026-10-27, step 2, session 2c; what the pack keeps chosen by the owner*

A pack now holds, besides maps, the world graph and the atlas, what the first milestone needs of the client's tables,
measured in [client.md](client.md), and only the texts they name. `GamePack.FormatVersion` is 3.

- **What it keeps.** Only what a harvesting and banking bot uses:
  - `items/catalog` (`ItemCatalog`): every item's id, name, type, level and weight in pods, and every type's name,
    `superTypeId` and `categoryId`, kept raw since the client names neither.
  - `jobs/catalog` (`JobCatalog`): the jobs, and every skill's name, job, gathered item, minimum level, `range` and
    `useRangeInClient`.
  - `interactives/catalog` (`InteractiveCatalog`): every interactive's name.
  - `banks/catalog` (`BankCatalog`): the 10 banks, as the map in front of each, and the 8 bankers with the replies that
    open the bank.
  - `world/atlas`: each sub-area gains its resources, the items professions gather that its `harvestables` lists.
  - `texts/<language>`: 26,411 texts per language instead of 339,912; the pack shrinks from 15.6 MB to 4.7 MB.
- **What links them.** The client's data says neither which interactive offers which skill nor which interactive an
  element of a map is: the server tells, element by element. It links a profession's skill to its resource, and the
  resource to the sub-areas listing it. A bot looks for a resource by item, through these links, then uses the elements
  the server says offer the skill.
- **Banks.** The client marks neither banks nor bankers. The extractor takes a bank for a hint drawn with graphic 401,
  and a banker for an NPC with a reply reading `Consulter son coffre personnel` in French; it keeps the replies, so a
  bot needs no text at run time, in any language. Talking is NPC action 3, `Banker.TalkAction`.
- **Layout.** Tables are packed unmanaged structs sorted by id, and a row's list, such as a sub-area's resources or a
  banker's replies, is a slice of one array of the catalog, as transitions are of the graph's; `Tables` holds what the
  catalogs and the atlas share. `Item` keeps the client's widths, 13 bytes for 21,554 items; the small tables keep
  `int`. `GameTables` reads each catalog once, when first asked for.
- **Checks.** The extractor fails when a table contradicts what D3B relies on: a record keyed under another id, a skill
  of a job the client lacks, a profession's skill gathering an item the client lacks or an item of no known type, a
  harvestable that is no item, a reply that is not a pair, no bank, a bank off the graph, no banker, a banker who
  cannot be talked to, a named text missing in a language. What a bot does not rely on is counted and kept: one test
  item of a type the client lacks, skills of `Base` naming item 0 or a missing item, kept as gathering nothing.
- **Rejected.**
  - Every NPC's name and actions, 6,472, for the live map of step 5: the first milestone needs only the bankers. A step
    that needs NPCs adds them.
  - Every hint, 834, workshops and marketplaces among them: steps 11 and 12 add what they need.
  - Items' `m_flags`, kept raw as maps' are: no bit is measured, and nothing would read it.
  - Every harvestable: the loot boxes a sub-area lists are no resource to look for.
  - The bank's inside, found at extraction: from 5 of the 10 banks a door leads to 2 or 3 maps at the bank's
    coordinates, and which holds the banker only the server says.
  - Bankers and replies written in the extractor by id: a client adding a banker would go unnoticed; the text finds
    him, and the extraction fails if it finds none.
  - Choosing the chest reply by its text at run time, as BillBot does: a bot would depend on the client's language.
  - One entry for every table: a catalog per domain keeps each type small, at no cost, since all are read once.

## D30: Still no zstd dictionary

*2026-10-27, step 2, session 2c, chosen by the owner*

D28 left the dictionary to be measured again once the texts shrank. On the pack of format 3, 4.67 MB with maps 80% of
it, a 16 KiB dictionary trained on the maps, deterministic, saves 0.45 MB, 9.6% of the pack, and makes every map
decompress in 2.3 µs instead of 3.5; the other entries gain nothing (client.md). Packs stay without one.

- **Why.** 0.45 MB a download does not pay for an entry every map depends on, so that a damaged dictionary loses them
  all, a field per entry in the index, and a training whose output must stay deterministic across zstd's versions. A map
  is read once, so faster decompression shows nowhere.
- **Rejected.**
  - A 16 KiB dictionary trained on the maps, which the lead session measured and did not recommend.

## D31: The host gets the pack of the installed build before it starts

*2026-10-27, step 2, session 2d. Builds D24's fetching.*

- **Flow.** `BotHost.RunAsync` builds the host, then, before starting it, calls `PackSource.EnsureAsync`: it reads the
  installed client's build (`ClientBuild`, from `Client:Directory`, by default where the launcher installs it), looks
  for its pack under `Data:Directory` (by default `%LOCALAPPDATA%\D3B\data`, D24), and otherwise downloads it from the
  release `client-<version>` of `Releases:Repository` (by default `AerafalDev/D3B`). The host then opens the pack, so that
  an unreadable one stops it before any service reads it. Without a pack for the installed build, or with the pack of
  another build under its name, the host logs a critical message that names the build and the command extracting its
  pack, does not start, and exits with code 1.
- **Download.** `ReleaseDownloader`, in `D3B.Data.Releases`, goes through GitHub's REST API, which serves private
  repositories: the release by its tag, then each asset by its identifier with `Accept: application/octet-stream`. The
  asset's URL is built from its identifier, never taken from the answer, and the token is set on each request to the
  API; `HttpClient` clears it when it follows GitHub's redirect to the bytes. The SHA-256 asset is read first. The pack
  is streamed into `<pack>.download` and hashed as it comes. It must match the SHA-256, then open as a pack of this
  format extracted from this build, before it is moved into place; a file that appeared there meanwhile, such as one
  the extraction tool wrote, is kept. Only a whole, checked pack is ever under the name the host looks for (D24).
- **Failures.** Every one ends as the host's message and exit code 1, never as a crash. A read waiting 30 s for its bytes
  fails the download, since `HttpClient`'s own timeout ends with the headers. A lock beside the pack, `<pack>.lock`,
  deleted when closed, keeps two hosts from downloading it at once: the second stops and says so. Ctrl+C during the
  download ends the process before the host has started: only `<pack>.download` is left, and the next start downloads
  again.
- **The data module.** `DataModule`, first of `DefaultModules`, registers singletons that every bot shares:
  `ReleaseDownloader`, `PackSource`, `ClientBuild`, `GamePack`, `GameTexts`, `GameMaps`, `GameWorld` and `GameTables`.
  The pack is opened once, by a factory, so that the container owns it and closes it with the host; the stores are safe
  for any number of threads. Nothing is read or downloaded while the module builds: composition stays synchronous and
  offline (D20), and the host downloads asynchronously, with its logs, before it starts.
- **Where it lives.** The module is in `D3B.Bot`, with the default modules (architecture), so that `D3B.Data`, which the
  tools reference, stays free of the Generic Host. `ClientRelease`, in `D3B.Data`, names a build's release, its assets
  and their checksum for the host and for the release tool (D33): `<asset>.sha256` holds one line as `sha256sum` writes
  it, and `sha256sum -c` checks it.
- **Rejected.**
  - Downloading while the module builds: network access in composition, synchronous, before the host has a logger.
  - Downloading in a hosted service: the Generic Host creates every hosted service before starting any, so a service
    taking a store in its constructor would open the pack before it is downloaded.
  - `IHttpClientFactory`, with Microsoft.Extensions.Http: a package for one download per client update.
  - Checking GitHub's own digest of the asset instead of the `.sha256` asset: it proves the download matches the upload,
    not that the upload matches what the maintainer extracted. The release tool compares it (D33).

## D32: The download's token comes from the configuration, else from the GitHub CLI

*2026-10-27, step 2, session 2d, chosen by the owner*

While the repository is private (D4), a download needs a GitHub token. `ReleaseToken` takes `Releases:Token` when the
host's configuration sets it, such as the user environment variable `Releases__Token`, and otherwise asks the GitHub CLI
for its logged-in account's token, `gh auth token --hostname github.com`: the owner is already logged in, so there is
nothing to configure.

- The token is asked for only when a pack must be downloaded, is never logged, and goes to GitHub's API only (D31).
  Without either source, the download goes without one: a public repository accepts it, and for a private one the host's
  message says where a token comes from.
- **Risk.** The CLI's token has the `repo` scope, wider than reading releases; it leaves the machine for GitHub's API
  only. The host starts `gh` as Windows finds a program by its name: beside the host, in the current directory, then on
  `PATH`, a program of the user's own environment. It gets 10 s, and none of `GH_TOKEN` and `GITHUB_TOKEN`, which it
  would print instead of its account's token.
- Once the repository is public, no token is needed; one still raises GitHub's rate limit.
- **Rejected.**
  - The configuration alone, which the lead session recommended: one place and no process started, but a token to set by
    hand.
  - Windows' credential manager: native code for Windows only, while D15 plans Data Protection for secrets at step 5.
  - `GH_TOKEN` and `GITHUB_TOKEN`: shared with other tools, and possibly holding another context's token.

## D33: Publishing a client build's release

*2026-10-27, step 2, session 2d*

`scripts/release.cs` publishes the pack of a build, the installed client's by default or `--client-version`'s, from
where the extraction tool writes it (`--packs`), to the release `client-<version>` (D24).

- **Checks.** It opens the pack, which checks its format and that it is whole, checks it was extracted from that build,
  and writes its SHA-256 beside it, in `data-<format>.pack.sha256`. Then, by the release's state:
  - no release: `gh release create` with both assets, titled `Client <version>`, with notes naming the build, the
    format, the fingerprint and the extractor, and `--latest=false`, so that D3B's own releases stay the latest;
  - a release without this pack, such as one made for an older format (D24): `gh release upload` of both assets;
  - a release holding this pack, by GitHub's digest of the asset: nothing to do;
  - a release holding another pack under that name: refused.
- **Never replaced.** A host keeps the pack it has and never downloads it again, so a published pack is fixed for its
  build and format: replacing it would leave hosts with two packs under one name. An extraction fix that changes the
  content of a published pack ships under a new format. The extractor's version is in the pack (D26), so another build of
  the extractor gives other bytes, which the tool refuses for a published pack.
- **Dry run by default.** Nothing changes on GitHub without `--publish`, which then compares GitHub's digest of the
  uploaded pack with its own SHA-256.
- **Through the GitHub CLI.** The maintainer's credentials stay in `gh`; the tool handles no token. CI builds the tool and
  cannot run it: it has no client (D24).
- **Immutable releases stay off** for the repository: a release gains assets after it is published, a newer format's
  pack, then step 3's mapping. The tool enforces what immutability would, by never replacing an asset.
- **Rejected.**
  - Steps in the docs only: the checksum must be in the format the host reads, which `ClientRelease` defines once for
    both.
  - The REST API from the tool: more code, and a second place holding a credential.
  - A GitHub Actions workflow: CI has no client.

## D34: A hosts line brings each client to D3B, which rewrites the game server's address

*2026-10-27, step 3, session 3a, chosen by the owner. Settles Q8. Superseded in part by D35: no `hosts` line, the
client's own configuration brings it to D3B; the game server's address is still rewritten.*

The player starts each account's client from the official Ankama launcher, as without D3B. One line of the Windows
`hosts` file sends the connection host, which the client's configuration names, to a loopback address of D3B's. D3B
listens there, resolves the real host without the `hosts` file, and connects to it. The game server's address reaches
the client in a message of that first connection: D3B rewrites it to an address of its own, and connects to the real
game server when the client arrives. Nothing in the client, its files or its process changes.

- **What the client shows**, measured in [client.md](client.md#connection). The launcher starts `Dofus.exe` with its
  own local port, an instance id, a hash and the connection port, 5555 or 443. The client reads the connection host
  from the JSON its `ConfigUrl` names, connects to it, a load balancer, then to a game server whose address the first
  connection sends. In game, a client keeps one game connection, on port 5555, besides the chat, the launcher and the
  CDN.
- **Who starts the client.** The player, from the launcher: the client asks the launcher for its login token, so
  starting it otherwise means emulating the launcher and handling Haapi, which D23 leaves to the official client.
- **The line** names the connection host and an address of `127.0.0.0/8` that D3B's configuration sets, not
  `127.0.0.1`, so that D3B takes no port another program uses there. D3B listens on that address on the configuration's
  ports, 5555 and 443, so the launcher's port setting does not matter. Writing the line needs elevation, once: a tool of
  session 3b adds, checks and removes it, through the UAC prompt. The host never runs elevated and never edits the file.
- **Resolving.** D3B resolves the connection host without the `hosts` file, which would send it back to itself; how is
  session 3b's choice. The tool reads the host's name from the configuration the client reads, the public JSON of its
  `ConfigUrl`; that is no game connection (D23).
- **Every client on the machine goes through D3B** while the line is there. A client without a bot is relayed
  unchanged. Without D3B running, no client of the machine connects: removing the line restores direct play.
- **The game server.** D3B decodes the connection server's message holding the game server's address and ports, which
  needs the protocol mapping, and replaces the address with its own and every port with its game listener's. It keeps
  the real address for that client and matches the client's next connection to it, by what that connection sends first;
  how exactly is session 3e's. Until then the game connection goes straight to the server, and D3B sees the connection
  phase only.
- **Reading.** The references read port 5555 in the clear, framed by a varint length (D3); the client's TLS transport,
  Spin, serves its chat, which D3B leaves alone with Haapi and the launcher. Session 3b measures the real bytes before
  anything relies on them.
- **Within D2.** D3B hides nothing: connections leave the same machine, from the same address, one per client, as
  without it. Nothing is injected into the client, patched or emulated.
- **Risks.**
  - Should the configuration name another connection host, clients would bypass D3B: the tool compares the line with
    the configuration each time it runs.
  - An antivirus may warn about a change to `hosts`.
  - Whether the client accepts a loopback address for its connection host is measured in session 3b.
- **Rejected.**
  - Editing `ConfigUrl` in the client's `version` file and serving the configuration from D3B: no elevation, but the
    client's installation changes, the launcher restores the file at each update, and whether the client accepts a
    local `http://` address is not measured.
  - WinDivert: no rewriting of the game server's address, but a third-party kernel driver loaded with elevation at every
    start, and address translation on packets.
  - Hooking the client's `connect`, as BillBot does through Frida, or patching its code, as JondoEmu does through
    MelonLoader and Harmony: they change the client's process or files (D2, D23).
  - Emulating the launcher to start the client, as BillBot and JondoEmu do: D3B would get the login token through Haapi
    (D23).
  - Playing without a client, as BubbleBot does: ruled out by D23.

## D35: The client's configuration brings it to D3B, rewritten by the host while it runs

*2026-10-27, step 3, session 3b, chosen by the owner. Supersedes D34's `hosts` line.*

The client reads its connection host from the JSON its version file's `ConfigUrl` names (client.md). While D3B's host
runs, that URL names a copy D3B writes, Ankama's configuration with every connection host replaced by D3B's address:
the client connects to D3B, and D3B knows the real host from Ankama's configuration. No `hosts` line, no elevation.

- **Measured**, on client 3.7.12.12 started from the launcher ([client.md](client.md#configuration)). The client loads
  the URL with `UnityWebRequest` at start; it refuses `http:`, even on loopback ("Insecure connection not allowed"), and
  reads a `file:` URL. The launcher started it with the changed file, without repairing it. The client then connected
  through D3B to the server choice and into the game.
- **Flow.** `ClientRedirect`, a hosted service of the network module, starts after the relay listens:
  1. It reads `ConfigUrl` from `Dofus_Data/StreamingAssets/version`. When the URL is already D3B's, as after a crash,
     Ankama's is the one kept from before.
  2. It downloads Ankama's configuration, or uses the last one read when it cannot, and fails the host's start when it
     has neither.
  3. It writes, in `Network:Redirect:Directory` (`%LOCALAPPDATA%\D3B\client` by default), `config.json` (the copy),
     `config-url` (Ankama's URL) and `ankama-config.json` (Ankama's configuration).
  4. It points `ConfigUrl` at `config.json` as a `file:` URL, changing that line and no other byte of the file.

  While the host runs, it watches the file: the launcher rewrites it when it updates the client, and the client is
  pointed at D3B again, a check failing on a file still being written trying again a few times. At stop, Ankama's URL
  goes back, so a client started without D3B plays directly; so it does when the host is disposed without being
  stopped, as when a later service fails to start. After a crash, the next start, or
  `dotnet run --file scripts/client.cs -- restore`, puts things right.
- **One connection host.** A connection carries no name the relay could tell hosts apart by: a configuration listing
  several fails the start. Each would need an address of its own. A connection host that is a D3B address, as another
  D3B's copy would name, fails the start too: the relay would connect to itself.
- **Resolving.** With no `hosts` line, the relay resolves the real host with .NET's own DNS, at each connection: it is a
  load balancer. For D34's line the owner had chosen `DnsQuery_W` with `DNS_QUERY_NO_HOSTS_FILE` and
  `DNS_QUERY_BYPASS_CACHE`, over the DnsClient package and a DNS client of our own; with no line to get around, it was
  removed.
- **Within D2.** One line of a configuration file of the client's installation changes while D3B runs, and the launcher
  rewrites it at each update. The client's code and process stay untouched, and nothing is hidden: every client of the
  machine started while D3B runs goes through it, from the same machine, as D34 said.
- **Risks.**
  - Ankama could make the client check its version file, or refuse `file:` URLs. Each client update measures it again:
    a client that does not reach D3B shows it at once.
  - A path holding spaces or letters outside ASCII is percent-encoded in the URL; whether Unity reads it back is not
    measured.
  - After a crash, clients cannot connect until the host starts again or the tool restores the URL; the tool's `check`
    says so.
  - Two hosts on one machine would share the file, and the first to stop would give clients Ankama's configuration
    back; nothing prevents it yet.
- **Rejected.**
  - D34's `hosts` line: elevation, and a change to the whole machine, which the owner refused once it was built. Its
    tool, writing the line through the UAC prompt, was removed before it was committed.
  - Serving the configuration from D3B over `http:`: the client refuses it, measured.
  - Over `https:` with a certificate of our own: a root certificate in the user's store, worse than a `hosts` line.
  - BillBot's way: it starts `Dofus.exe` under Frida, hooks `ws2_32!connect` to send port 5555 to itself, stands in for
    the launcher's local service and gets the login token from Haapi (D2, D23).
  - The URL rewritten by a tool run by hand, which the lead session laid out as the other option: the configuration goes
    stale, and clients cannot connect while D3B is stopped.

## D36: D3B.Network relays the connection phase

*2026-10-27, step 3, session 3b*

- **Kestrel without a web host.** `BotBuilder` wraps the Generic Host's builder (D20), which has no web host, so
  `RelayServer`, a hosted service, runs Kestrel's own `KestrelServer` on its socket transport. Each endpoint ends in a
  connection handler and serves no HTTP; `KestrelServer.StartAsync` still takes an HTTP application, which nothing
  calls. Kestrel brings what the relay needs: connection ids in the logs, limits, and a graceful stop.
- **Where.** `Network:Address`, 127.0.13.59 by default, whose last two bytes read `0x0D3B`: in 127.0.0.0/8 and not
  127.0.0.1, so that D3B takes no port another program holds there (D34). `Network:Ports`, 5555 and 443, the
  configuration's ports. A port D3B cannot listen on, held by another program or reserved by Windows, stops the host,
  with a message naming the address and the setting to change, and exit code 1.
- **Each connection.** `RelayConnectionHandler` finds the real server through `ClientRedirect` (D35), connects to its
  addresses in turn, 10 s each, on the port the client chose, through `SocketConnectionContextFactory` (D8), and copies
  each direction as it reads: bytes as they are, flushed at once, whatever the framing. It holds no state between
  connections, so any number of clients are relayed at once; a test relays eight.
- **Closing.** A side closing completes the other's output, which Kestrel's socket transport sends, then shuts down both
  ways: the relay keeps no half-open connection. A side still open 5 s later is cut. When the host stops, every
  connection is cut, and one still looking for its server stops looking.
- **The network module**, in `D3B.Bot` after the data module, registers the relay and the redirect; captures record the
  installed build from `ClientBuild` when the data module is there.
- **Rejected.**
  - An accept loop of our own over `IConnectionListenerFactory`: Kestrel's server already manages connections and their
    stop.
  - `WebApplication`'s builder: `BotBuilder` and plugins build on the Generic Host's (D20).
  - Half-closed connections: Kestrel's socket transport shuts down both ways once an output completes.

## D37: Captures record raw reads, in a format of our own

*2026-10-27, step 3, session 3b, chosen by the owner. Settles the format of Q7; replays with injection are session 3f's.*

- **What.** The bytes each side sent, read by read, both ways, and each side closing, timed in microseconds from the
  start. Never decoded messages: they follow from the raw bytes and the build's mapping, so an old capture decodes again
  with a newer one.
- **Format.** One file per connection, `.d3bcap`. A header: the magic `D3B-CAPT`, the format version, the start, the
  installed client's version, the listener, the server's host and address. Then records, appended as they come: a kind,
  the offset as a varint, and for bytes their length as a varint and the bytes. Each record is flushed: a capture whose
  host was killed reads up to its last whole record, and a record cut short is reported as such. A write that fails ends
  the capture, never the relay.
- **Where.** Off by default; `Network:Captures:Enabled` records every connection in `Network:Captures:Directory`,
  `%LOCALAPPDATA%\D3B\captures` by default. A capture holds the account's login ticket: it stays on the machine, is never
  committed, never shared.
- **Replay.** `CaptureReplay` writes each record into a `Pipe` as its own write and flush, so a reader gets the bytes cut
  where the network cut them (D8). Tests build their own captures with `CaptureWriter`.
- `scripts/capture.cs` describes captures by their structure: reads, frames, sizes, TLS or not, and with `--frames` the
  protobuf shape of each frame, field numbers, wire types and lengths, never a value.
- **Rejected.**
  - pcapng: IP and TCP headers to make up, for Wireshark, which does not decode Dofus.
  - JSON lines with Base64: 1.4 times larger and slower, readable for protobuf no one reads by eye.
  - MemoryPack and zstd, as packs: a whole object written at once, where a capture grows as the connection goes, and
    would be lost in a crash.
  - Frames instead of reads: it would depend on the framing being measured, and lose the cuts a reader must handle.

## D38: The connection is varint-framed protobuf, in the clear

*2026-10-27, step 3, session 3b*

Measured on the connection phase of two clients through the relay ([client.md](client.md#the-connection-phase-on-the-wire)):
no TLS, every byte in varint-length frames, every frame a protobuf message.

- `FrameReader.TryReadFrame` reads a frame from a `SequenceReader<byte>`: a length of at most five bytes, 32 bits, then
  the frame, sliced from the sequence without a copy. It consumes nothing until a frame is whole, so the caller reads
  more and tries again, and a length that cannot be one, or above 16 MiB by default, throws: no telling where the next
  frame starts. `FrameWriter` writes them.
- **Benchmark.** 10,000 frames of the measured sizes and of 4,000 bytes: about 16 ns a frame on one segment, 23 ns on
  segments of 4 KiB and 36 ns on segments of 512 bytes, with no allocation (BenchmarkDotNet, short run,
  `benchmarks/D3B.Network.Benchmarks`).
- **Rejected.**
  - Reading a length byte by byte with no bound, as some references do: a misread length would make a reader wait for,
    and buffer, gigabytes.

## D39: The obfuscated schema is a protobuf FileDescriptorSet

*2026-10-27, step 3, session 3c, chosen by the owner*

A client build's obfuscated schema, every message and enum of its protocol under their obfuscated names, is a
`FileDescriptorSet`, protobuf's own description of `.proto` files, serialized: `schema-<format>.binpb`, format 1.

- **Where.** Beside the build's pack, in `%LOCALAPPDATA%\D3B\data\<version>\`, written by `scripts/schema.cs` (D40).
  Derived from the client, it is never committed (D11): it ships as an asset of the release `client-<version>`, with its
  SHA-256 beside it (D24). It is published with the build's mapping in session 3e, since nothing downloads it before;
  until then the maintainer keeps the schemas of the builds measured.
- **Why.** Google.Protobuf builds descriptors from it and links every type name to its type, so a schema that does not
  hold together is never written. Session 3d compares it with the descriptors of the readable `.proto` files, which
  Grpc.Tools compiles: one model on both sides, the one D15 planned for the translation's tables. protoc reads it too.
- **Files.** One per protocol assembly, `connection.proto` and `game.proto`, in proto3 and without a package, since the
  client's types have no namespace; `google/protobuf/any.proto`, which `game.proto` imports, is in the set. A map is
  the entry message protobuf describes it with, a proto3 `optional` field has the synthetic oneof protoc gives it.
- **Provenance.** A descriptor set has no place for it: the directory and the release name the build. The schema is
  deterministic, two runs on one client giving the same bytes, so writing it again checks it. A change in what the tool
  writes for a client whose schema is published bumps the format, as for packs (D33).
- **To read it**, `--proto <dir>` also writes it as `.proto` text, which protoc compiles back to a descriptor equal to
  the schema, field by field (measured); it stays on the machine, never committed.
- **Rejected.**
  - A JSON document of our own: readable and diffable, with its provenance inside, but a copy of `descriptor.proto`, and
    session 3d would convert one side to compare it with the other.
  - The `.proto` text as the artifact: the most readable, but .NET has no `.proto` parser, so every reader would run
    protoc.
  - An entry of a D3B pack, its provenance in `PackInfo`: a model of our own again, and the pack's format is the data's.

## D40: The schema is read from the client's metadata, and its encodings from the machine code

*2026-10-27, step 3, session 3c*

`scripts/schema.cs` opens the client's `GameAssembly.dll` and `global-metadata.dat` with Cpp2IL.Core
2022.1.0-pre-release.21, in process (D15), and reads `Ankama.Dofus.Protocol.Connection` and
`Ankama.Dofus.Protocol.Game`, measured in [client.md](client.md#protocol-code).

- **Metadata version 39**, the session's first risk: Cpp2IL reads it, the client opening in about 5 s. Pre-release.21,
  of 2026-02-22, is still Cpp2IL's latest. JondoEmu's notes record that Il2CppDumper refuses version 39; not measured
  here.
- **The structure, from the metadata.** Protobuf's C# generator gives every message the same members, whose shape
  the obfuscation keeps:
  - a message is a type implementing `IBufferMessage`;
  - its `int` constants are its field numbers, declared in field order: the n-th is the number of the n-th field
    property, a property with a setter, or a `RepeatedField` or `MapField`, whose type gives the field's C# type;
  - a getter-only `bool` property right after a field's says whether the field is set: a proto3 `optional` field, or a
    scalar member of a oneof;
  - a oneof is a getter-only property typed by an enum nested in the message, whose values but zero are its fields'
    numbers;
  - the message's own messages and enums are in a static class nested in it, protobuf's `Types`.

  Any other member fails the reading: a member the generator does not write means a message this reading would
  misread.
- **The encodings, from the machine code.** A C# `int` may be an `int32`, a `sint32` or a `sfixed32`, which differ on
  the wire. Cpp2IL lifts each message's `InternalWriteTo` and static constructor to its instruction-set-independent
  language, and the tool follows the constants loaded into the argument registers: a field of one value is written as
  `WriteRawTag(tag)` then `Write<Type>`, and a list of scalars through a codec, `FieldCodec.For<Type>(tag)`. The tag
  holds the field's number and wire type, so an encoding goes to its field by number, never by order. Every field of
  one value must be written once, by a method fitting its C# type, and every codec must belong to a field, which also
  checks how numbers pair with properties.
  - A map's key and value codecs have the same tags in every map, so one is matched by its C# type, and one with an
    encoding other than that type's default fails the reading: which map is its could not be told.
  - Codecs of messages and enums are generic, and Cpp2IL leaves their calls unresolved: the metadata types those lists,
    and a list of enums is taken as packed, proto3's default; a reader accepts either.
  - Registers are followed only while their value holds on every path: a call forgets those it may change, and an
    instruction a jump lands on forgets them all.
- **Measured** on 3.7.12.12: every field read agrees with its code; the protocol uses only `int32`, `int64`, `uint32`,
  `bool`, `string`, `float`, enums and messages, and packs every list of scalars. The schema, 170,287 bytes, is written
  in about 6 s, the same bytes on two runs.
- **Checked against the captures.** `scripts/capture.cs --schema <file>` finds, among the schema's messages, the one
  typing the most fields of every frame of the captures, both sides, and with `--frames` names every field. On session
  3b's three captures, one message types all 356 fields of the 8 frames, the next at most 8: the connection's root,
  whose fields 1 and 2 are the client's and the server's messages, and theirs 3 and 4 the two exchanges
  ([client.md](client.md#the-connection-phase-read-by-the-schema)).
- **Rejected.**
  - Writing the assemblies to disk and reading them with `MetadataLoadContext`, as JondoEmu does: 69 MB written per
    build, for the metadata Cpp2IL's contexts give in process.
  - The types of the C# properties alone, as JondoEmu's `.proto` writer reads them: an `int32` and a `sint32` look the
    same there.
  - The client's 102 original files: a message's static `Descriptor` getter names its file's class and its index, but
    enums have no such getter, and 69 top-level enums could not be placed without guessing. One file per assembly.
  - The client's own serialized descriptors, which would give its schema whole: they are not in clear in the metadata,
    and finding where they are, perhaps encrypted with the obfuscator's strings, is a study of its own. Not done.

## D41: Step 3 after session 3c: the connection's mapping first, the game's after its captures

*2026-10-27, step 3, session 3d, chosen by the owner*

The roadmap's session 3d held the readable `.proto` files of the connection phase and of the first milestone's
messages, the generated classes, the scored matching, the pinned pairs, the contract audit, the IDA tracer and the
anchors: too much for one session. It is split, and the sessions after it renumbered:

- **3d**: the connection phase's mapping, end to end: its readable `.proto` files and classes, the matching, the pins,
  the audit, the anchors from session 3b's captures, the mapping's format, and the tool writing a build's mapping
  (D42 to D45).
- **3e**: Q2 and the version gate for the connection phase, the mapping and the schema published in the build's release,
  the game server's address rewritten, so that D3B holds both connections and captures the game connection.
- **3f**: mapping, part 3: the first milestone's game messages, with anchors from 3e's captures of the game connection.
- **3g**: Q9 and replays with injection, the former 3f.
- **Why this order.** The game messages' anchors need captures of the game connection, which goes straight to the game
  server until 3e rewrites its address; and 3e needs the connection phase's mapping to decode the message holding it.
- **The IDA tracer** waits for a measured need, in 3f at the earliest. It traces which fields the client's code reads,
  which BillBot uses to tell fields of one shape apart; D40 already reads every field's encoding from the machine code,
  and on the connection phase the structure alone pairs every type (D43).
- **Rejected.**
  - The roadmap's 3d as written: the game messages would have no anchor, their connection not going through D3B yet.
  - The tracer now: the connection phase leaves nothing for it to settle.

## D42: The readable protocol of the connection phase

*2026-10-27, step 3, session 3d*

Bot code reads the connection phase through classes Grpc.Tools 2.85.0 generates at build, with Google.Protobuf (D11,
D15), from five `.proto` files of our own in `src/D3B.Protocol/Connection/`: `connection`, `identification`, `servers`,
`accounts` and `friends`, package `d3b.connection`, C# namespace `D3B.Protocol.Connection`, no gRPC service. They hold
37 messages, 9 enums and 92 fields, one for each of the build's connection file (D39).

- **Numbers are the wire's.** The obfuscation renames messages, fields, enums and values and keeps numbers: 3.7.12.12's
  connection file has the messages and field numbers of `dofus-unity-proto`, an older build's, but where this build
  changed or added a field ([client.md](client.md#the-connection-phase-mapped)). So a readable class reads the
  connection's frames as they are; whether the translation then decodes once or twice stays Q2's, in 3e.
- **Names** come from the references where this build's shape is theirs, same number and type and, for an enum, as many
  values, written in our own words (D3). Elsewhere they are neutral, and the comment says what is not measured: the
  four rights flags, where the references have five or six; the values of the identification's failure reason, 15
  where they have 14, of the server selection's error, 5 where they have 4, and of the server type, new, 7. A field or
  value is renamed once its meaning is measured: their names are in neither the wire nor a mapping's contract (D45).
- **What is not on the wire is ours.** Files, nesting and declaration order are chosen to read well; Ankama's classes
  come from one file. A message field carries no `optional`: the generated C# gives such a field no `Has` property, so
  the schema cannot show its presence (D40).
- `ReadableProtocol` lists the files, the connection's anchor plan (D43), the schema file each package is matched in,
  and the digest of the protocol's contract (D45).
- **Rejected.**
  - The references' files (D3; `dofus-unity-proto` has no license), or Ankama's single file.
  - Numbers of our own: every field would need translating, for nothing.
  - Names guessed from the references where the build changed: a wrong name misleads more than a neutral one.

## D43: Matching: pins, anchors from captures, fields, then scores

*2026-10-27, step 3, session 3d*

`ProtocolMatcher`, in `D3B.Protocol.Mapping`, pairs every readable message and enum with its counterpart in a build's
schema; `scripts/mapping.cs` runs it on the installed build. Ideas from BillBot (scores, pins) and JondoEmu (anchors
from real captures), the code our own (D3).

- **Evidence, strongest first.** The build's pins; the anchors its captures give; the fields of every pair, a field
  being its counterpart's field of the same number when it has its shape (label, map or not, kind of type), which pairs
  their types; then structural scores for what is left, followed through fields again. Pairs are one to one both ways,
  and the first pair a type gets stays: later evidence that disagrees is a conflict, which fails the mapping. Nothing is
  settled by a guess.
- **Scores**, from 0 to 1: the share of fields, on both sides, with the same number and shape, a field of a paired
  type counting only if the types are each other's. A pair is accepted at 0.8 or more, the best of its row and of its
  column by 0.1 or more. Two empty messages score 1, so only where they are used tells them apart.
- **Scope.** A readable package is matched within one file of the schema, `d3b.connection` within `connection.proto`:
  the client keeps the connection phase and the game in two assemblies (D40). Packages matched in one file compete for
  its types together.
- **Anchors**, measured in the build's captures by the structure of their frames only (D37, D38): the root is the
  message of the connection file that reads every frame and types the most of their fields, alone; each frame holds one
  of its fields, the client's `request` and the server's `response`, the field a side's first frame holds, other
  fields, such as the server's events, being passed over; in a side's first frame holding it, the member of a oneof
  the frame holds is `identification`, in its second `server_selection`. A frame contradicting another leaves the
  anchors it touches out, and the tool fails until the plan or the readable protocol is fixed.
- **Pins** are committed, one file per build, `src/D3B.Protocol/Pins/<client version>.json`, protobuf's JSON form of
  `PinnedPairs` (`pins.proto`): a readable message or enum and its obfuscated name, and the fields whose numbers differ.
  A build's pins stay when the next build comes, so that its mapping can be made again. 3.7.12.12 needs none: its file
  is empty. A pin of a field the readable message lacks is a conflict, the tool fails without the directory of pins,
  and a test checks that every pin names its build, a readable type and its fields.
- **Measured on 3.7.12.12**: with the anchors, 37 messages paired, 7 by the captures and 30 by fields, and 9 enums by
  fields; from pins and structure alone, the same pairs, 29 messages and 4 enums by scores
  ([client.md](client.md#the-connection-phase-mapped)).
- **Risks.**
  - Fields pair by number, which the obfuscation keeps. Should a build shuffle the numbers of fields of one type, the
    structure could not tell them apart: values in captures, the IDA tracer or pins would.
  - The tool reads every capture of the build. Once 3e captures the game connection, no message of the connection file
    reads its frames: 3e must keep the connection phase's captures apart.
- **Rejected.**
  - BillBot's signals and weights, runtime confidence, IDA accesses and capture order among them: built for 2,000
    messages, and the connection phase needs none.
  - JondoEmu's matching from one build's dump to the next: structure settles each build on its own, so far.
  - Matching every readable type against the whole schema: 2,213 candidates where the right file has 37.

## D44: The contract audit

*2026-10-27, step 3, session 3d*

`ContractAudit` checks a mapping against the readable protocol and the build's schema. It fails when a readable
message or enum has no counterpart, or one shared with another; when a readable field has none, or one that does not
fit: another number than the mapping's, another type (through the mapping for messages and enums), label or presence,
or another oneof, the members of a readable oneof having to be those of one oneof of the counterpart, and of no other
readable oneof; when a readable enum value has none; when the mapping pairs a field or a value twice, or one the
readable type lacks; and when the mapping names another format, schema or readable protocol. What the build has beyond the readable protocol is noted, not failed: a reader meets it as unknown fields,
which fail loudly when they carry a value (D11).

- **Where it fails the build.** The mapping tool runs it and, on any failure or conflict (D43), exits with code 1 and
  writes nothing. In 3e, the host runs it on the mapping it loads and does not start on a failure, D11's version gate.
  CI has no client and no schema: it runs the audit's tests, on schemas made by renaming the readable protocol and on a
  fixture of our own, checks every pin, and `MappingFormatTests` fails when the readable protocol's contract changes
  without a new mapping format (D45).
- **Measured on 3.7.12.12**: 37 messages, 92 fields, 9 enums and 60 values checked, no failure and no note: the readable
  protocol reads every field and value of the connection phase.
- **Rejected.**
  - An audit in `dotnet build` or `dotnet test` against the installed client: CI has none, and a test reading the
    machine's client is not reproducible.
  - BillBot's contract, a list of required messages with score thresholds: the readable protocol is the contract,
    whole.

## D45: The mapping is a protobuf message of our own

*2026-10-27, step 3, session 3d, chosen by the owner*

A build's mapping is `mapping-<format>.binpb`, a serialized `ProtocolMapping`, which `mapping.proto` defines in
`src/D3B.Protocol/Mapping/`: its format, the client version, the schema's SHA-256 (D39), the readable protocol's digest
and the tool's version; then every readable message by full name with its obfuscated full name, the evidence that paired them
(D43) and its fields as pairs of numbers; and every readable enum with its values likewise.

- **Where.** Beside the build's pack and schema, in `%LOCALAPPDATA%\D3B\data\<version>\`, written by
  `scripts/mapping.cs` aside then moved. Derived from the client, it is never committed (D11): it ships as an asset of
  the release `client-<version>` with its SHA-256 (D24), published with the schema in 3e.
- **Format.** `MappingFile.FormatVersion`, 1, changes whenever `mapping.proto` or the readable protocol's contract does,
  so that each version of D3B reads its own mappings, side by side, and a published one is never replaced (D33). The
  contract is `ProtocolDigest`: every message and enum by full name, every field's number, type, label, presence and
  oneof, every value's number; names of fields, values and oneofs, and comments, are not part of it.
  `MappingFormatTests` records it per format, as `PackFormatTests` does for packs.
- **Measured.** The mapping of 3.7.12.12 is 3,305 bytes, the same bytes on two runs of one build of the tool, whose
  version it records, as a pack records its extractor's (D33); the tool takes 0.2 s once built.
  `--json` prints it to be read, never to be committed.
- **Why.** One model with the schema (D39), with Google.Protobuf already there; generated code reads and writes it,
  deterministically; it pairs numbers both ways, so it serves either answer to Q2.
- **Rejected.**
  - JSON of our own, which the lead session laid out: readable as it is, but a second model beside the descriptors.
  - The schema annotated with custom options naming the readable counterparts: one artifact, but the schema's format
    and the mapping's tied together.

## D46: Session 3e split: the translation and the gate, then the game connection

*2026-10-28, step 3, session 3e, chosen by the owner*

D41's session 3e held Q2, the version gate, the schema and mapping published, the game server's address rewritten, the
game connection relayed and captured, the connection phase decoded and recorded, and measurements on running clients:
two decisions, and work both with and without clients. It is split, and the sessions after it renumbered:

- **3e**: Q2 decided and built for the connection phase, the version gate, the schema and mapping published by the
  release tool, and the relay decoding the connection phase and logging it (D47 to D50). Everything is checked without a
  client, so in CI.
- **3f**: the game connection: the server selection's answer rewritten to a game listener of D3B, the client's next
  connection matched with the real game server, relayed and captured apart from the connection phase, which
  `scripts/mapping.cs` reads (D43), and what client.md leaves open measured on running clients: port 443, whether the
  client takes an address for the game server's host, how it chooses among the ports, and which side speaks first on
  the game connection. That last measurement decides how a connection is matched: by the ticket it presents, or by a
  listening endpoint of its own per server selection. It is decided once measured, not before.
- **3g**: mapping, part 3, the first milestone's game messages, D41's 3f.
- **3h**: Q9 and replays with injection, D41's 3g.
- **Why.** 3f decodes the server selection's answer, which needs the mapping the gate loads and a relay that reads
  frames: both are 3e's. 3e ends reviewable without a client; 3f needs running clients and a decision of its own.
- **Rejected.**
  - Everything in one session, as D41 planned.
  - The gate and publication alone in 3e, the translation with the game connection in 3f: the translation is what the
    gate checks a mapping for.

## D47: The translation decodes once, through the readable classes

*2026-10-28, step 3, session 3e, chosen by the owner. Settles Q2.*

The readable classes read a build's wire as it is: the obfuscation keeps every field's and value's number (D42), and
the connection phase carries no type name. `ProtocolTranslation`, in `D3B.Protocol.Translation`, exists only for a build
whose mapping holds, and decodes through it.

- **At the gate.** `ProtocolTranslation.Create` audits the mapping (D44), then requires every readable field and enum
  value to have its own number on the wire. A build renumbering one is refused, each such field named: its mapping holds
  both numbers (D45), so rewriting them can be added for the messages that change, once one does and is measured.
- **At run time.** A frame is parsed once, its unknown fields dropped, and is understood only when its message encodes
  back to the frame's length: one that does not holds a field or a value the readable protocol lacks, or was encoded
  otherwise. D3B passes it on as it is, and never rewrites it, so that 3f rewrites only frames understood whole, and
  loses nothing doing so. An enum value the readable enum lacks keeps its number and encodes back as it came, so it goes
  undetected and nothing of it is lost; the audit notes the values a build adds (D44), none in 3.7.12.12.
- **The game**, from 3g: the `Any` envelope's type URL names the obfuscated message, which the mapping turns into the
  readable one, then parsed the same way.
- **Measured.** The 8 frames of session 3b's captures decode through the readable classes, hold no unknown field, and
  encode back to the same bytes; so do the 4 frames of a client logged in through the host in this session (client.md).
- **Rejected.**
  - Rewriting field numbers on the wire, both ways: an encoder of our own and a second pass and copy of every frame, a
    changed tag size changing every enclosing length, for renumbering no build has shown.
  - Decoding twice, through the obfuscated descriptors then into the readable classes: Google.Protobuf's C# has no
    dynamic message, so a decoder and a copier of our own, and two trees per message.

## D48: The version gate

*2026-10-28, step 3, session 3e. Builds D11's gate.*

- **The protocol module.** `ProtocolModule`, in `D3B.Bot.Protocol`, requires the data module and registers
  `ProtocolSource` and the build's `ProtocolTranslation`, a singleton every bot shares. `DefaultModules` holds the data,
  protocol and network modules, in that order, and the network module requires the protocol module: a relay never runs
  without the gate, and disabling the protocol module has no effect while the network module is there (D20).
- **Flow.** After the pack (D31) and before the host starts, `BotHost` calls `ProtocolSource.EnsureAsync`, then asks for
  the translation. The schema and the mapping are looked for beside the build's pack, `schema-1.binpb` and
  `mapping-1.binpb`; one missing is downloaded from the release `client-<version>`, checked against its SHA-256 and that
  it is a schema, or a mapping naming this build, before it is moved into place. The translation then loads both, checks
  that the mapping names the installed build, audits it and checks its numbers (D47). Any failure logs a critical message
  naming what is missing or what fails first, and the commands that map the build, and the host exits with code 1.
- **Mapping a new build.** The gate never needs a running host: a new build's connection phase is mapped from its
  structure alone (D43), so `scripts/schema.cs`, then `scripts/mapping.cs`, both without a capture, map it, and the host
  starts. The mapping tool refuses what the host would, renumbered fields included, so that it never writes a mapping
  the host does not start on. Captures of the game connection, from 3f, need the connection phase's mapping only, which this gives.
- **Shared names.** `SchemaFile` moves from `scripts/unity/` to `D3B.Protocol.Mapping`, beside `MappingFile`, so that the
  schema tool, the mapping tool, the release tool and the host name the schema one way. The data module registers its
  `ReleaseToken` for both downloads.
- **Rejected.**
  - The relay running without the protocol module, raw, as in 3b: it would bypass D11's gate, and 3f cannot rewrite the
    game server's address without decoding.
  - Failing on the build's version alone, without the audit: a mapping from another version of D3B's readable protocol,
    or matched with another schema, would pass.

## D49: The relay reads frames, and logs the connection phase by what it is

*2026-10-28, step 3, session 3e. Superseded in part by D51: the observer becomes an interceptor, which may have a frame
held.*

- **The seam.** `IRelayObserver`, in `D3B.Network.Relay`, sees every whole frame of every relayed connection, with the
  connection's id and the side that sent it. `D3B.Network` stays free of the protocol; the network module registers the
  observer.
- **Forwarding** is D36's: bytes as they are read, at once. With an observer, each side's bytes are also read with
  `FrameReader` (D38): each whole frame is shown, the start of one not yet whole kept until the rest comes. A side whose
  bytes are no varint frames is observed no more, with a warning; so is a side once the observer has thrown, which is
  logged as an error and never reaches the connection.
- **The connection phase.** `ConnectionPhaseDecoder`, in `D3B.Bot.Network`, decodes each frame through the translation
  (D47) and logs it by the readable fields its oneofs hold and its length, such as `request.identification.token, 126
  bytes`, at the information level; a frame not understood is a warning naming why. Never a value: the phase carries the
  account's login token and ticket. That supersedes the architecture's "logged with the raw bytes": the bytes are in the
  captures, when they are on, which stay on the machine (D37).
- **Measured** on a client logged in through the host: the 4 frames of its connection phase decoded and understood
  (client.md).
- **Rejected.**
  - Forwarding frame by frame, each frame held until whole, which the lead session built first: bytes that are no frames
    but read as a plausible length, such as a TLS stream on port 443, which is not measured, would be held until that
    many came, and the connection would hang. 3f holds the one frame it rewrites, the server selection's answer, with a
    way out of its own.
  - Snapshots of decoded messages with Verify, which the architecture planned from 3e: the tests compare what is logged
    with what they built, and snapshots wait for bot states, at step 4.

## D50: The release tool publishes the schema and the mapping

*2026-10-28, step 3, session 3e. Extends D33; publishes what D39 and D45 planned for this session.*

`scripts/release.cs` publishes a build's pack, schema and mapping, `data-3.pack`, `schema-1.binpb` and
`mapping-1.binpb`, each with its SHA-256 beside it, to the release `client-<version>`.

- **All three or nothing.** A host needs all three (D31, D48): a missing one stops the tool with the command that writes
  it, exit code 2.
- **Checks** before anything is published: the pack as D33 checks it; the schema loads; the mapping names the build and
  holds, as `ProtocolTranslation` requires (D47).
- **By the release's state**, each asset on its own: absent, it is uploaded; present with the same GitHub digest,
  nothing to do; present otherwise, or with its checksum missing, or a checksum without its asset, the tool refuses,
  before uploading any. A new release is created with all six files, its notes naming each asset's SHA-256 and where it
  comes from.
- **Never replaced** (D33): the mapping records its tool's version (D45), so a mapping made again by another build of
  the tool has other bytes, and the published one is kept. A change in what a tool writes ships under a new format.
- **Dry run by default**, as D33: nothing changes on GitHub without `--publish`. Measured on 3.7.12.12: the release does
  not exist yet, and would be created with the three files, which hold.
- **Rejected.**
  - Publishing whichever files are there: a release holding a pack without its mapping, which no host can start on.

## D51: The relay holds the server selection's answer alone, and rewrites it once understood whole

*2026-10-28, step 3, session 3f. Supersedes in part D49: the observer becomes an interceptor, which may have a frame
held.*

- **The seam.** `IRelayInterceptor`, in `D3B.Network.Relay`, replaces D49's `IRelayObserver`. The relay asks it for an
  `IConnectionInterceptor` per connection, with the route of the listener that took it (`RelayRoute`: the connection
  phase or the game); none leaves the connection unwatched. The connection's interceptor sees every whole frame, both
  sides, and says whether a side's next frame is held. `D3B.Network` stays free of the protocol.
- **Holding.** A frame is held only when the interceptor asks for it before any of its bytes has gone, and its length is
  within `RelaySettings.MaxHeldLength`, 64 KiB: its bytes wait until it is whole, then what the interceptor returns goes
  in its place, its new length before it. Every other byte goes as it is read, as in D49.
- **The way out.** A frame held goes on as it came, and its side holds nothing more, when it is not whole after
  `RelaySettings.HoldTimeout`, 5 s, or when its side closes; bytes that are no frames, or an interceptor that throws,
  end the watching of the side, as in D49. A stream that is no frames but reads as a plausible length costs that delay
  once: the hang D49 feared is bounded.
- **What is held.** `ConnectionPhaseSession`, each connection phase's interceptor, has the server's frames held from the
  client's `request.server_selection` until the server's `response.server_selection`. The relay shows a frame to the
  interceptor before flushing it to the other side, so the hold is asked for before the server can answer. Events in
  between are held too, each going on once whole.
- **The rewrite.** Only an answer understood whole (D47), and a success: its host becomes D3B's address and its ports
  the game listener's (D53), the real host, ports and ticket going to `GameServerSelections` (D52); the message is
  encoded again through the readable classes, the rest as it was. Any other answer, one not understood, or one the relay
  could not hold whole goes on as it came, with a warning: the client then goes straight to the game server, as before
  this session. Captures keep what each side sent, the answer as the server sent it.
- **Measured** on clients of 3.7.12.12 started from the launcher: the answer, 83 bytes, came in one read, was rewritten,
  and the client went to D3B's address (client.md).
- **Rejected.**
  - Telling the answer by the first bytes of a frame not yet whole: a reader of partial protobuf, where the client's
    request already says which answer comes next.
  - Holding every frame of the connection phase: D49's risk, for nothing.

## D52: The game connection is matched by the ticket it presents

*2026-10-28, step 3, session 3f, chosen by the owner. Settles what D34 and D46 left to this session.*

- **Measured** (client.md): on the game connection the client speaks first, as soon as it is connected and unprompted,
  and the server one round trip later. That first frame, 76 bytes, holds the ticket the selection's answer gave, 32
  characters, as a string four fields down: so did every game connection measured, four.
- **How.** The game listener reads the client's first frame before connecting to any server, leaving it in the pipe,
  within the hold's bounds (D51): whole within 5 s, 64 KiB at most. `GameServerSelections` looks for the ticket of a
  selection waiting among the frame's length-delimited values, which `ProtobufValues` finds by the wire format alone, so
  no mapping of the game is needed. The selection found is taken, and the relay connects to its host, resolved then, on
  the real port behind the game port the client chose; the frame is then relayed and recorded first.
- **Failing closed.** A first frame presenting no ticket waiting, none whole in time, or bytes that are no frame: the
  connection is closed with a warning, and nothing reaches a server. A selection waits 60 s
  (`RelaySettings.SelectionLifetime`), is taken once, and its ticket is kept in memory only, never logged.
- **Several clients at once.** Two clients of two accounts, started together, went into the game through D3B, on two
  game servers, each matched by its ticket.
- **Risk.** A build whose server spoke first would have every game connection closed after 5 s: it would show at once,
  and the measurement is made again at each client update. A listening endpoint per selection would then be the way.
- **Rejected.**
  - A listening endpoint per selection, an ephemeral port listed alone, which the lead session laid out: no reading, and
    whoever speaks first, but a listener per selection outside Kestrel's fixed endpoints, and a port any local program
    could connect to first.
  - The oldest selection waiting, which the measurements used: two clients selecting at once could be crossed, which
    step 10 cannot have.
  - The ticket's bytes searched anywhere in the frame: the wire says where each string starts and ends, a search does
    not.

## D53: The game connection is relayed on a port of its own, unread, and captured apart

*2026-10-28, step 3, session 3f*

- **The listener.** `RelayServer` also listens on `Network:Game:Ports`, on D3B's address, each endpoint handing its
  connections to the relay with its route. One port by default, 5556: the client connects to the first port it is sent,
  whatever its connection port setting (client.md). The answer lists a game port for each real port, as far as the game
  listener has them, each standing for the real port at its position: one. A port the relay cannot take stops the host,
  as in D36.
- **Unread.** The connection phase's decoder gives no interceptor for the game connection, whose protocol is the game's,
  read from session 3g: its bytes go as they are, both ways.
- **Captures apart.** The game connection's captures go to `game`, in the captures' directory
  (`RelaySettings.GameCapturesDirectory`). The mapping tool and `scripts/capture.cs` read the files of the directory
  they are given, not of its subdirectories, so the connection phase's anchors never meet the game's frames (D43);
  `scripts/capture.cs <captures>\game` describes the game's.
- **Measured**: four game connections of 23 to 32 s, every byte in varint frames of protobuf in the clear, both sides.
  The mapping tool, run again with seven captures of the connection phase beside four of the game in `game`, read the
  seven and paired the same types.
- **Rejected.**
  - One listener for both connections, told apart by their first frame: the connection phase's server is reached
    before its client speaks.
  - A field of the capture's header naming the route: a new capture format, where a directory leaves the tools as they
    are.
  - A second address for the game listener: the ports already tell the routes apart.

## D54: The game is paired by pins where its structure is silent, then from build to build

*2026-10-28, step 3, session 3g, chosen by the owner. Supersedes in part D43: at the game's scale, one build's structure
no longer settles it alone.*

Measured on 3.7.12.12's schema and session 3f's four captures of the game connection
([client.md](client.md#the-game-read-by-the-schema)):

- The game file holds 2,175 messages, 1,372 of which no field is typed by: the messages frames carry. 758 of these have
  a shape no other has; 117 are empty, 73 hold one `int32`, 48 one `bool`. Of the 63 messages the first milestone needs,
  as the references name them, 2 have a shape the build holds once, 24 one it no longer holds, and 37 one shared with 2
  to 175 of its messages.
- Neither the client's code nor its metadata names them: the classes handling the game's messages are obfuscated too,
  and the 510 original names the metadata holds are bare strings, tied to no type.

So:

- **Pins** pair what the structure and the captures leave open, committed per build (D43): for 3.7.12.12, the 43
  messages the frames carry that the readable protocol reads, but the ticket's request, which the captures anchor.
  Every message they hold is then paired by its fields.
- **Proposed by the tool.** `scripts/mapping.cs` lists, for each readable message left unpaired, the build's messages it
  fits, those the captures carry first, with the side carrying them. The maintainer pins from what the player did; the
  tool pins nothing.
- **Scores** still run, within the game's file, accepted only clear of every rival (D43): without pins they paired 9 of
  3.7.12.12's messages, each as pinned.
- **From build to build**, later: a build's schema and mapping pair the next build's, by the order and shape of their
  messages, once a second build is installed to measure it on (Q10). Until then, a client update stops the host until
  the game is pinned again (D48).
- **Rejected.**
  - Anchors from scripted captures, the order of the player's actions: every update replays them, and the client sends
    many other requests between.
  - A study of the client's code first: the classes using the messages are obfuscated, measured.
  - Build to build now: no second build to measure it on.

## D55: The game's events read only what bot code uses; its requests are whole

*2026-10-28, step 3, session 3g, chosen by the owner. Amends D47 for the game.*

- **Measured.** The map's information holds, through its fields, 142 messages, 36 enums and 679 fields; the ten largest
  events on entering the game, 171 messages and 779 fields.
- **Partial.** A readable message marked `option (d3b.readable.partial) = true` (`src/D3B.Protocol/Readable/options.proto`)
  declares only the fields bot code reads, their meaning measured, and leaves its counterpart's others out on purpose. A
  message the client sends is never partial, so that the bot builds it whole: the mapping tool fails when the captures
  show the client sending one.
- **The audit** fails as before on what a readable message declares (D44). It counts what a partial message leaves out
  apart from its notes (`AuditReport.Unread`), so that a note stays what a build added.
- **The translation.** A game frame is read when its envelope is understood whole and its message's declared fields
  read; understood whole when its message also encodes back to its length (D47). Bot code may use a frame read; D3B
  rewrites only a frame understood whole, and passes the others on as they came.
- **The gate** fails only when what bot code reads no longer fits: a field a build adds or changes elsewhere is counted.
- **Rejected.**
  - Every message whole, through all it holds: hundreds of messages to name for the milestone, and every change Ankama
    makes to a field the bot never reads would stop the host.

## D56: The readable protocol of the game

*2026-10-28, step 3, session 3g*

Files of our own in `src/D3B.Protocol/Game/`, package `d3b.game`, C# namespace `D3B.Protocol.Game`, matched within the
schema's `game.proto` (D43): `game`, the envelope, then one file per domain of the first milestone, `authentication`,
`characters`, `context`, `maps`, `movement`, `interactives`, `inventory`, `jobs`, `npcs` and `exchanges`. With the
connection phase's, the readable protocol holds 106 messages, 217 fields, 17 enums and 128 values; the game's carried
messages are 44, 33 messages of the game are partial (D55).

- **The envelope**, measured: every frame is a `GameMessage`, whose oneof holds a `GameResponse` at 1, a `GameEvent` at 3
  and a `GameRequest` at 4; its field 2, of the event's type and outside the oneof, is never set. A request and a
  response carry an `int32` and a `google.protobuf.Any`, an event an `Any` and a list of `int32` never set. The client
  sends requests only; the server events, and one response per connection.
- **The Any.** Its type URL is `type.ankama.com/` and the build's name of the message. The obfuscation leaves protobuf's
  own types as they are (D39), so `google.protobuf.Any` is its own counterpart, by its full name, for the matcher, the
  scores and the audit, and no mapping pairs it.
- **Numbers are 3.7.12.12's.** In the game file, 980 of the 982 messages of three fields or more number them 1 to n.
  Against the references' older build, 15 of the 18 messages that pair alone by the kinds of their fields number them
  otherwise, where the connection's keep theirs (D42). 110 of the game's messages hold two fields of one message type,
  of which the captures set one, and the metadata names types `decoy`. So the game's fields are likely shuffled and
  renumbered, with decoys, at each build. The readable numbers stay this build's: a mapping holds both numbers (D45), and
  the translation will rewrite them once a build renumbers, as D47 provides, measured on that build (Q10).
- **Names** as D42: from the references where the meaning is measured, in our own words, neutral elsewhere. A meaning is
  measured in captures, by structure and timing, and by values compared on the machine with the build's pack, such as an
  element's id with its map's elements; no value is written down.
- **Rejected.**
  - Numbers of our own and a rewriter now: fields could no longer be paired by number, which is the next build's work,
    with no second build to measure it on.

## D57: The game's anchors, and the messages its frames carry

*2026-10-28, step 3, session 3g. Extends D43.*

- **The plan.** `ReadableProtocol.GameAnchors`: every frame is the root, the client's frames are its requests, the
  server's its events, and a connection's first request carries the ticket's `AuthenticationRequest`, as every game
  connection did (D52). Nothing else is anchored: what follows depends on the player. An `AnchorPayload` names the
  message a side's n-th frame carries in an `Any`.
- **Type URLs.** The anchors read them, since they name the schema's types, and no other value. Every `Any` of every frame
  must name a message of the schema's file that reads its value whole, or the measurement reports it and the tool fails.
- **What the frames carry** is counted by side (`AnchorMeasurement.Payloads`), for the tool's proposals (D54).
- **Where.** The tool reads a captures directory's own files as the connection phase's, and its `game` directory as the
  game's (D53).
- **Measured** on 3f's four captures and one of this session's: 1,069 frames, whose root types 4,280 fields, the next
  1,912; anchored, the envelope, the request, the event and the ticket's request; 150 messages carried, 37 by the client
  and 113 by the server, each read whole as its type URL names it.

## D58: The translation reads a game frame through its type URL

*2026-10-28, step 3, session 3g. Builds what D47 planned for the game.*

- `ProtocolTranslation.DecodeGame` parses the envelope once, then the message its `Any` carries: the type URL's name is,
  through the mapping, a readable message, whose parser reads the value. `DecodedGameFrame` gives the envelope, the
  build's name, the readable type, the message, and whether the frame is understood whole or read (D55).
- A frame carrying a message the readable protocol lacks is named and not read; bytes no envelope reads have none.
- The relay does not read the game connection yet: bot state needs it at step 4, and nothing rewrites a game frame
  before Q9.
- **Measured** by the mapping tool, which decodes the game's captures with the mapping it just wrote: of 1,069 frames,
  178 understood whole, 69 more read, 822 carrying one of 106 messages the readable protocol lacks, none misread
  ([client.md](client.md#the-game-mapped)).

## D59: Mapping format 2

*2026-10-28, step 3, session 3g*

The readable protocol's contract changed with the game: `MappingFile.FormatVersion` is 2, written to `mapping-2.binpb`
(D45); `mapping.proto` is unchanged. A host of this version looks for `mapping-2.binpb` beside the pack, else in the
build's release (D48), which the release tool publishes (D50).

## D60: The bot sends the requests that start an action, and the client does what follows

*2026-10-28, step 3, session 3h, chosen by the owner. Settles Q9.*

Measured on the six captures of the game connection, then on a client of 3.7.12.12 in game through the host, D3B
sending 18 requests of the first milestone's eight kinds itself ([client.md](client.md#the-game-acted-on)):

- **Request ids.** Every request of the first milestone, the client's and D3B's, carries the id -1; so does every
  response. The client counts other requests, which the readable protocol lacks: in a session of 26 minutes, nine of
  four kinds, numbered 2 to 10 in turn: one sent once after the character list, one about every four minutes after two
  events of the server, and two together after 24 minutes. D3B's requests left that count as it was.
- **What the client does.** The server answered each request D3B sent as it answers the client's own, and the client
  showed it: the character walked, changed maps and harvested; the banker's dialog opened, then the bank, the stack and
  the kama moved into it, and the window closed. The client also sent what follows an action of its own: after each of
  five walks, the confirmation once arrived, 0.46 to 1.77 s later, which the server answered; after each of two map
  changes, the request for what is on the map, with its twin, 0.72 to 0.86 s later; once the server closed the bank D3B
  left, two requests to leave, within 40 ms, which the server did not answer. The player kept playing between D3B's
  requests.
- **What the server refuses.** A path that does not start where the character stands: one event, and no walk. A use it
  refuses: two events. A use of an element that the map's data places elsewhere than the server's state does: nothing.
  The readable protocol lacks these events.

So:

- **What D3B sends**: the requests that start an action, `MapMovementRequest`, `MapChangeRequest`,
  `InteractiveUseRequest`, `NpcActionRequest`, `NpcDialogReplyRequest`, `ExchangeTransferFromInventoryRequest`,
  `ExchangeKamasRequest` and `DialogLeaveRequest`, each whole (D55) and with the id -1, through the game session (D62).
  Never what the client sends after an action, the confirmation of a walk or the request for a map's information, and
  never a counted request.
- **Staying consistent.** The server's answers reach the client as they came, and the client follows them as after its
  own actions: no frame of the game is held, dropped or rewritten, and nothing is sent to the client. The bot learns
  that an action is over from what the server answers, such as the response to the client's confirmation, the map's
  information, or the end of a use; a refusal, or nothing, ends it after a delay. Bot code reads them at step 4, which
  names the refusals it needs in the readable protocol (D55).
- **The player** keeps the client: its own requests go on as before. How the bot and the player share the character is
  step 4's.
- **Within D2.** D3B sends the client's own requests, built whole, through the client's own connection; it hides
  nothing and imitates no timing.
- **Risk.** A client update could stop the client from following an action it did not ask for: the bot would then wait
  for a confirmation that never comes. The measurement is made again at each update, with the session of Q10.
- **Rejected.**
  - The bot driving every step, as BillBot does: its own confirmation after a walk's duration it computes, the client's
    then dropped by the relay. A duration formula, frames dropped, and every request the client sends after an action
    to know and to imitate, where the client sends them itself.
  - Counting D3B's requests: no request of the first milestone is counted.

## D61: D3B's frames go to the server between the client's, and captures record them

*2026-10-28, step 3, session 3h. Extends D37, D49 and D51.*

- **The seam.** `IRelayInterceptor.Intercept` takes the connection, `IRelayConnection`: its id, its route, a token
  cancelled once it closes, and `SendToServerAsync`, which sends the server a frame of D3B's own. Only towards the
  server: nothing D3B does sends the client a frame.
- **Where a frame goes.** The client's read loop and a sender take a gate in turn, the loop from a read's bytes to their
  flush. A frame goes at once when what went to the server ends on one of the client's frames, else as soon as the
  client's frame in progress has gone whole, so that the server reads every frame whole. Frames sent one after another
  go in that order; a sender that stops waiting withdraws a frame not gone. A connection closed, unwatched, or whose
  client's bytes are no frames, fails the frame with `RelayInjectionException`: where a frame could go is not known.
- **Captures, format 2.** A record of a new kind, `Injected`, holds each frame D3B sent, with its length, as it went:
  the server got the client's records and these, in their order. A format 1 capture reads as before.
  `CaptureReplay` gives them to a pipe of their own when asked, and leaves them out otherwise, as a replay through the
  relay, which sends its own, needs. `scripts/capture.cs` describes them apart; the mapping tool reads the client's own
  frames only, so its anchors are the client's (D57).
- **Measured.** The 18 frames D3B sent went whole between the client's, each a record of its own, and the server read
  every one: it answered all but the use it ignored (D60).
- **Rejected.**
  - Waking the client's read loop to write a frame, as the hold's timer does (D51): a cancelled read meaning one more
    thing, where a gate needs none.
  - Recording D3B's frames as the client's: a capture would say the client sent what it did not, and a replay through
    the relay would send them twice.

## D62: The game connection is read through the translation, for the modules that observe it

*2026-10-28, step 3, session 3h. Builds what D58 left to Q9.*

- **The relay's interceptor**, `RelayDecoder`, gives each connection phase its `ConnectionPhaseSession` (D49, D51) and
  each game connection a `GameSession`. It replaces `ConnectionPhaseDecoder`.
- **A game session** decodes every frame once through `DecodeGame` (D58), shows it to every `IGameObserver` in the
  services, and logs it at the debug level by the readable message it carries, or the build's name, its length and
  whether it was understood or read, never what it holds. It holds and rewrites nothing. An observer sees the session
  start, then each frame of each side, from the relay's read loops, so it must be quick and safe across threads, as
  posting to a bot's inbox is (D9); what it throws is logged and goes no further.
- **Sending.** `GameSession.SendAsync` encodes a readable request with `ProtocolTranslation.EncodeGameRequest`, then
  sends it through the relay (D61) and logs what it sent. The translation encodes only a message of the game that the
  frames carry, paired by the build's mapping and not partial: the envelope with the id -1, and an `Any` whose type URL
  names the build's message.
- **Measured.** The plugin that measured D60, never committed, was such an observer: it sent its requests through the
  sessions it was shown.
- **Rejected.**
  - A registry of sessions: an observer is shown each one as it starts.
  - A queue of frames per session: frames would pile up where no bot reads them.

## D63: Replays with injection

*2026-10-28, step 3, session 3h. Settles Q7, with D37.*

Tests build a game session as a capture, read by read, some frames cut across reads, and replay it through the relay,
read by a game session whose observer sends a request once the server said what is on the map. Each side's record goes
once the other side has all that was sent before it, D3B's request included, so every run is the same. They check that
the server gets the client's frames with D3B's request whole between them, that the client gets the server's bytes as
they came, that the observers see every frame both sides sent and none of D3B's, and that the relay's capture of the
replay gives each side what it got. The relay's own tests cover where a frame goes: between frames, after one in
progress, in order, withdrawn, refused.

- **Rejected.**
  - Replaying the captures of a real account: they hold its ticket and are never committed (D37).
  - Snapshots of decoded messages: what they would hold, the tests compare with what they built (D49); bot states are
    compared at step 4.

## D64: Step 4 in six sessions

*2026-10-28, step 4, session 4a, chosen by the owner*

The roadmap's step 4 holds the actor, the state, behaviors, interruption, recovery and reconnection, the sharing of the
character with the player, the server's answers and refusals, cell and world pathfinding, moving between maps, using
interactives and the bank: too much for one session. It is split:

- **4a**: the actor and walking on a map: one inbox per bot fed by an `IGameObserver`, a game connection tied to its
  account and its character, the character's state and its snapshots, how the bot and the player share the character,
  cell paths in `D3B.Engine` measured on the captures, the walk with the server's answer to the client's confirmation,
  and its refusal named in the readable protocol (D65 to D70).
- **4b**: between maps: world pathfinding over the graph's vertices by zone, scrolls, scrolls with an action and map
  actions, with the criteria the state answers; changing maps, what the bot waits for, and its refusals named.
- **4c**: interactives: using an element from the nearest cell the character reaches, harvesting, the use's refusals
  named, doors as transitions of the world path; line of sight here only if measuring shows using depends on it, else
  at step 9.
- **4d**: the bank: the banker's dialog, the exchange with the storage, stacks and kamas, leaving, the way to the
  nearest bank.
- **4e**: zaaps and havens: their messages, which the readable protocol lacks, from captures of the player using them
  and pins, then in the world path.
- **4f**: activities, interruption, recovery and reconnection: behaviors chained, a fight starting, a disconnection,
  full pods and the player as interruptions, the recovery step, the client's new connection taking the bot back.
- Until step 5's app, no command comes from outside: tests drive the bots, and the measurements on the host go through a
  measuring plugin never committed, as in session 3h.
- **Rejected.**
  - Interruption, recovery and reconnection right after 4b: they need the behaviors of 4c and 4d to chain.
  - The bank with zaaps and havens in one session: two sets of messages to measure and map.

## D65: One bot per character, its account known by the ticket

*2026-10-28, step 4, session 4a, chosen by the owner*

A bot plays one character of one account (`CharacterKey`: the account's id and the character's). It is made when a game
connection's server confirms the character the client chose, and outlives its connections: when the client connects
again and chooses the character, the new connection takes the old one's place, and the state starts over from what the
server sends.

- **The account, by the ticket.** The connection phase's identification names the account (`account_id`, its nickname and
  tag), then the server selection gives the client a ticket, which the game connection's first request presents (D52).
  The connection phase's session remembers the account by that ticket in `GameAccounts`, and the game session takes it
  from the ticket of its `AuthenticationRequest` (`GameSession.Account`). An account waits as its selection does, 60 s,
  and is taken once. The ticket stays in memory, never logged.
- **A connection whose account D3B does not know**, such as one whose identification was not understood, gets no bot:
  it is only watched, with a warning.
- **Why.** The state is a character's; step 5's profiles depend on the character's professions and level; and step 4f's
  reconnection resumes where a character's state allows, which a bot per connection could not.
- **Rejected.**
  - One bot per account: its state would start over at each change of character.
  - One bot per game connection: no continuity after a reconnection, which 4f would have to rebuild under another key.
  - The account carried by the relay, as a tag of `GameServerSelections`: `D3B.Network` stays free of the protocol, and
    the game session reads the ticket anyway.

## D66: The player wins, and the bot pauses until it is resumed

*2026-10-28, step 4, session 4a, chosen by the owner. Superseded in part by D81: leaving a dialog is the player's while
the character is in one.*

D3B neither holds nor drops a frame of the client (D60), so the player can always act. A request the player sends that
acts on the character interrupts the bot's behavior (D9), and the bot pauses: it starts no behavior until it is resumed,
by step 5's app, by tests until then.

- **What is the player's** (`PlayerActions`): an observer never sees D3B's requests (D62), so every request it sees is the
  client's. The requests that start an action are the player's: the eight kinds D3B sends (D60), and the stop of a walk,
  which the client sends when the player gives a new path during one, measured 24 times, each followed at once by the new
  path (client.md).
- **What is not**: what the client sends by itself after an action, its own or D3B's: the confirmation of a walk, the
  request for a map's information, the two requests to leave once the server closed an exchange (D60). Leaving a dialog
  is not counted yet: telling the player's from the client's needs the dialog's state, which session 4d adds.
- **Only while the bot acts.** The player acting while no behavior runs pauses nothing: the next behavior is an explicit
  command anyway.
- **Busy.** A behavior starts nothing while the character walks: the walk answers `Busy` (D69).
- **Measured** on a client in game through the host: the player gave a new path 300 ms into a walk D3B sent, the stop
  and the new path came, and the bot was interrupted and paused.
- **Rejected.**
  - Resuming by itself after some time without the player's requests: the bot would start again while the player reads,
    thinks, or is in a menu, which sends nothing.
  - The bot ignoring the player and planning again from the new state: the two would fight over the character.

## D67: The bot's run time

*2026-10-28, step 4, session 4a. Builds D9.*

- **The actor**, `BotActor` in `D3B.Bot.Runtime`: one unbounded `Channel<T>` read by one loop, so its state needs no lock.
  Its inbox gets the messages of its character's game connection, both sides, posted by `Bots` from the relay's read
  loops, and its loop turns each into the next state (`CharacterEvents`), then tries it on the behaviors' expectations.
  The class is not named `Bot`: inside every `D3B.*` namespace the name would be the `D3B.Bot` namespace.
- **The state**, `CharacterState` in `D3B.Bot.Characters`: an immutable record, replaced whole by the loop and read by
  anyone, holding what the server said: the character's name and level, its map, sub-area and cell, the path it walks,
  its inventory by stack, kamas, weight and most weight, and its professions with their levels and skills.
  `CharacterEvents.Apply` is a pure function from a state and a message to the next state: the state handlers, tested on
  their own.
- **Behaviors** are async methods given a `BotContext`: the state's snapshot, the pack's maps, the clock, the log,
  `Expect` and `SendAsync`. `BotActor.RunAsync` runs one at a time, only while a game connection plays the character and
  the player has not taken it over. An interruption cancels it, its token cancelled off the loop so that no behavior code
  runs on it, and it ends with `BotInterruptedException` and its reason: the player, the connection closing, the host
  stopping. The caller cancelling its own token is no interruption.
- **Expectations.** A behavior expects an answer before it sends the request: both go through the inbox in order, so no
  answer can come before the loop looks for it. The loop tries each message on the expectations once the state took it
  in, and an expectation ends with its behavior. A wait's delay goes through the bot's `TimeProvider`.
- **The registry**, `Bots`, an `IGameObserver`: it ties a game connection to its character's bot once the server confirms
  the choice (D65), raises `Added` for a character's first bot, and stops every bot with the host. The bots module
  (`BotsModule`) registers it, after the data and network modules, which it requires; `DefaultModules` holds it.
- **Tests** drive game connections as the relay shows them, on a pack of one map, with `FakeTimeProvider`, and wait for
  the loop through an internal `ProcessedAsync`.
- **Rejected.**
  - Behaviors run on the bot's loop, through a synchronization context of its own: a behavior awaiting on the loop it
    needs to go on would hang it, and D9 asks only that the state have one writer.
  - A bounded inbox: the relay's read loops must never wait for a bot.

## D68: Cells and paths in D3B.Engine

*2026-10-28, step 4, session 4a. Superseded in part by D86: on a map with stairs, a step changes floor by 10 at most but
along a stair.*

Measured on the eight captures of the game connection, 797 cells of the character's paths, and checked against the pack
(client.md):

- **Geometry** (`MapGeometry`, in `D3B.Engine.Maps`): a cell is its row times 14 plus its column, odd rows lie half a cell
  east of even ones, and directions run from 0, east, clockwise to 7. Every cell of the character's paths is walkable and
  the neighbour of the one before it, and the 310 turning points of the requests leave in the direction this gives.
- **Coordinates** (`MapPoint`): the server names a cell by two coordinates, x the column plus half the row rounded up, y
  the column less half the row rounded down, measured on its three refusals of a path (D69).
- **Paths** (`CellPathfinder`): A* over the eight neighbours a character may walk on, the destination walkable and the
  start anywhere. A step across, east, south, west or north, passes between two cells, one of which at least must be
  walkable: the client's paths never pass between two that are not, though the server walked one of D3B's that did.
  Steps are weighed by the time measured on the client's paths, running, 170 ms east or west, 110 north or south and 113
  along a diagonal: 3, 2 and 2. The estimate is the cost of the quickest path with nothing in the way. On the 67 paths the
  client asked for, these paths are as quick by these weights on 61 and quicker on 6, never slower, and the same cells on
  20. Nothing is kept between calls (D9's rejection of a shared pathfinder).
- **Turning points** (`CellPath.TurningPoints`): the first cell and each where the direction changes, with the direction
  leaving it, then the last with the direction arriving on it, as the client's requests name them.
- `D3B.Engine` references `D3B.Data` for the cells; its tests check the geometry, and with CsCheck (D15), on 100 random
  maps, that every path found is valid and as quick as an exhaustive search finds.
- **Rejected.**
  - Steps weighed alike: a path of fewer steps across the map is slower when its steps go east or west.
  - Passing between two cells that cannot be walked on, which the server allowed once: the client never does.
  - A benchmark of cell paths: 560 cells take microseconds; world pathfinding, at 4b, is where one pays.

## D69: The first behavior, walking on a map, and its refusal named

*2026-10-28, step 4, session 4a. Supersedes in part D60: the server's refusal of a path is an event of 40 bytes naming
the character's cell, not of 25.*

- **The walk** (`MapWalk`, in `D3B.Bot.Movement`): from the cell the state gives, a path in the pack's cells (D68), sent
  as the client sends its own, each turning point the direction times 4096 plus the cell (D60); then the server's answer,
  the client's confirmation once arrived, and the server's response to it, after which the character stands on the
  path's last cell. D3B never confirms: the client does (D60).
- **Outcomes** (`WalkOutcome`): arrived, on the response to the confirmation or already there; busy, while the character
  walks or its cell is not known, nothing sent; no path in the pack, nothing sent; refused; no answer from the server
  within 3 s; not confirmed within 1 s and 500 ms a step. Measured: the server answers within 150 ms, the client confirms
  0.38 to 2.9 s after the request, a step taking at most 186 ms running and 491 ms walking, and the server answers the
  confirmation within 100 ms.
- **The refusal**, named `MapMovementRefusedEvent`: a path that does not start where the character stands. One event,
  carrying the character's cell as coordinates (D68), 29 ms after the request, nothing else: measured three times, each
  naming the cell the character stood on. During a walk, the server places the character on the walk's first cell; a
  refusal then stops the client's walk there. The state takes that cell.
- **The stop**, named `MapMovementCancelRequest`: the client names the cell where the character stopped, then sends the
  player's new path from it, in the same read; the server does not answer. The state takes that cell (D66).
- **Pins and mapping.** The structure cannot tell either from the build's messages of its shape, 83 and 84 of them: the
  mapping tool proposed each among the 6 and 9 of these the captures carry, and they are pinned for 3.7.12.12 (D54). The readable protocol
  holds 108 messages and 220 fields: `MappingFile.FormatVersion` is 3, `mapping-3.binpb`, audited without failure.
- **Measured** on a client of 3.7.12.12 in game through the host, the player's hands off: the bot walked eight times
  through the walk behavior, 1 to 16 steps, each arrived in 0.41 to 1.84 s, the server walking every path exactly as
  sent; the path D3B sent from another cell was refused, naming the character's cell.
- **Rejected.**
  - A duration computed per step to end the walk, as BillBot does: the client's confirmation and the server's response say
    when it is over (D60); the delays only bound a wait that went wrong.
  - Waiting for the confirmation the client sends: the server's response to it is what makes the walk over for the
    server.

## D70: Bot states compared with snapshots written in the tests, not Verify

*2026-10-28, step 4, session 4a. Supersedes in part D15: the "Verify.XunitV3" part of the tests row.*

D15 chose Verify for snapshots, and the architecture planned to compare bot states with snapshots from step 4. Verify
33.3.4, the version D15 named, now fails the build without a SponsorCheck license property: a maintenance fee, or an
exemption its publisher defines and the consumer claims. Choosing one is the owner's call, not a test's.

- **So** a test serializes the state with `System.Text.Json`, indented, and compares it with the snapshot it holds as a
  raw string: the expected state reads in the test, and a change shows as a diff of text. Tests of the other handlers
  compare the fields they change.
- CsCheck 5.0.0, D15's property tests, and Microsoft.Extensions.TimeProvider.Testing join the tests' packages.
- Should the owner claim an exemption or sponsor, Verify can come back: the snapshots are text already.
- **Rejected.**
  - Claiming Verify's open-source exemption on the owner's behalf: a license decision.
  - `Verify_SponsorshipLicenseIgnored`: the build would knowingly breach the package's license.

## D71: Paths through the world in D3B.Engine, their criteria answered strictly

*2026-10-28, step 4, session 4b; the criteria rule chosen by the owner*

Measured on the world graph of 3.7.12.12 ([client.md](client.md#world-graph)):

- **The search** (`WorldPathfinder`, in `D3B.Engine.World`): Dijkstra's over the graph's vertices, each map change
  costing the same, to the nearest of the maps sought (`WorldPath`, its `WorldStep`s naming each transition by index).
  No estimate: map actions lead anywhere, so no distance between maps bounds what is left. Built once per pack, which
  reads the 619 criteria once and finds every vertex's sub-area and area; each search keeps its state in its own arrays
  (D9). The bots share one, built when one first travels.
- **What it follows**: scrolls, scrolls with an action and map actions. Doors are session 4c's, zaaps 4e's (D64). From
  the bank of Astrub this reaches 4,021 of the 11,327 vertices; with doors whose criteria are met it would be 5,551, and
  9,024 with every transition.
- **Criteria** (`Criterion`, `CriterionFacts`, in `D3B.Engine.Criteria`): each read once into a tree and answered on
  three values (`CriterionAnswer`): a group of `&` fails as soon as one condition fails, a group of `|` is met as soon as
  one is, and what is left is unknown. The syntax is measured: two-letter codes, an operator and a value, at times a
  context in brackets or a `#` before a group, and no group mixing `&` and `|`. The bot answers the codes its state holds
  (`CriterionCodes`): the level, the objects held, the professions, the subscription (D72), the map, sub-area and area,
  and the cell. Their meaning is the references' and the client's class names as they read them, not measured on the
  server; every other code, 40 of the 47 the graph names, is unknown.
- **Strict.** A transition is followed only when its criterion is met: unknown closes it, as false does. From Astrub it
  costs 67 vertices against ignoring criteria, 4,021 against 4,088.
- **The place.** A criterion is answered on the map its transition leaves. A condition on the cell counts as met while the
  path is planned, then names the cells the map is left from (D73).
- **Benchmark** (`benchmarks/D3B.Engine.Benchmarks`, on the installed build's pack, or `D3B_PACK`): a path 3 changes
  long in 2.2 µs, the farthest map in 154 µs, a map the search cannot reach, which explores everything reachable, in
  176 µs; about 100 KB a search. A journey can find its path again after every map change (D74).
- **Rejected.**
  - Following a transition whose criterion is unknown, then leaving it out once it fails: the bot would try passages a
    quest closes. The owner chose strict.
  - Ignoring criteria.
  - A* with a distance between the maps' coordinates: map actions jump across the world, so it would overestimate.
  - Weights by transition type or by the walk across each map: nothing measured says one change costs more than another.
  - A table of every pair of vertices: memory for nothing, at under a millisecond a search.

## D72: The account's subscription, read from its identification, prudently

*2026-10-28, step 4, session 4b, chosen by the owner*

- **Measured.** The connection phase's identification already holds `subscription_end` (D42): on the 11 captured, of
  two accounts, an ISO 8601 date with its offset, both accounts subscribed. No account without a subscription was measured.
- **Kept.** `GameAccount.SubscriptionEnd` holds the date, or nothing when the text is no date, and the state takes it
  (`CharacterState.SubscriptionEnd`). `IsSubscribedAt` answers by the bot's clock: subscribed while the end is ahead. An end
  not known counts as not subscribed.
- **Where it counts.** A path enters a sub-area the client closes to accounts without a subscription (`basicAccountAllowed`
  false: 533 of 567 sub-areas, 10,675 of 11,327 vertices) only when the account is subscribed, and `PZ` answers it (D71).
  What else an account without a subscription may not do is for later steps (roadmap).
- **Rejected.**
  - An end not known counting as subscribed: the bot would aim at zones the server closes to it.
  - Leaving the subscription for later: a path would cross subscribers' zones for every account.

## D73: Changing maps

*2026-10-28, step 4, session 4b*

Measured on the eight captures of session 4a and three of a client of 3.7.12.12 in game through the host, the player's
hands off, the bot asking for 30 map changes and travelling ([client.md](client.md#the-game-between-maps)):

- **Where a map is left from** (`WorldPathfinder.ExitCells`): a map action from its cell; a scroll from the walkable cells
  of its zone on the edge in its direction that the client marks as leaving it straight, or along a diagonal next to it
  but on a corner, and from the transition's own cell, which lies on that edge. Scrolls leaving one zone in one direction,
  to several zones or maps, share the edge by the nearest of their cells; a condition on the cell names a scroll with an
  action's own cells. The client left from such cells, the transition's own once in 12; the server took an edge cell
  marked nowhere, and ignored a request from a corner marked for the other edge and from a cell off the edge. Every
  scroll keeps at least its own cell, 18.4 on average.
- **The behavior** (`MapChange`, in `D3B.Bot.Movement`): the walk of D69 to the nearest of those cells, then, for a
  scroll, `MapChangeRequest` naming the transition's map, the neighbour even where the server sends the character
  elsewhere; for a map action, the walk onto its cell, stepping off first when the character stands on it. The bot then
  waits for the new map, 3 s, and for what is on it, 10 s, which the client asks for itself (D60). The new map is
  expected before the walk: the server's map actions cover cells next to the graph's, 424 for 409 and 154 for 140, so a
  walk to an edge may change maps by itself, and a request then goes unanswered.
- **No refusal to name.** The server answers nothing to a map change it refuses: 6 of the 30, from a cell off the edge,
  for the neighbour in another direction, for a map that is no neighbour, once the character was already elsewhere, and
  from a corner. Once, after a walk the client never confirmed, it answered with the refusal of D69, naming the walk's
  last cell. The readable protocol gains no message, its mapping stays format 3, and the change ends after its delay
  (`NoAnswer`).
- **The client draws the map first.** A walk the server showed 1 or 50 ms after a new map's information was never
  confirmed, though the server walked it: the server then has the character at its end, while the bot's state keeps it
  walking. Every walk sent 100 ms (twice), 200, 500 or 1,000 ms after was confirmed, and the player's own came 207 ms
  after at the soonest. The change is over 500 ms after the character is placed. This is no timing shaped to look human
  (D2): the client must have drawn the map to follow what the server shows it.
- **Outcomes** (`MapChangeOutcome`): changed, or elsewhere, once placed; busy and no path, nothing sent; not walked;
  refused, kept for a refusal a later measure finds; no answer; not placed.
- **Rejected.**
  - Every walkable cell of the edge, which the server took once: the client's marks are where its own player leaves, and
    a corner showed the server reads them there.
  - Waiting for a sign from the client that it drew the map: it sends none, measured.
  - A pause before every walk: only a new map needs one.

## D74: Travelling from map to map

*2026-10-28, step 4, session 4b*

- **The behavior** (`WorldTravel`, in `D3B.Bot.Movement`): the path from the vertex the character stands in to the maps
  sought, its first step (D73), then the path again from where the character stands, until it is on one of them. A scroll
  with an action may send it elsewhere, two zones may share an edge, and a search costs under a millisecond (D71).
- **Failures.** A change that fails leaves its transition out of the rest of the journey: its criterion may not be met
  for a reason the bot cannot know, or the server may refuse it silently (D73). The journey fails after 3 failed changes,
  after twice the first path's changes and 10 more, or when where the character stands is no longer known.
- **What the criteria are answered on** (`WorldTravel.FactsOf`): the state's level, objects by item, professions'
  levels and subscription at the bot's clock (D72).
- **Measured.** Through the host, with the player's hands off: journeys of one change; one of 6 changes in 27 s, one
  of them refused from a corner, left out, and the path found again through another map; one of 7 in 21.7 s, none
  failing. The first journeys, before D73's two fixes, failed on a request left unanswered after a walk to the edge had
  set off a map action, and on a walk the client did not confirm 1 ms after a map's information.
- **Rejected.**
  - Following the first path to its end: where a change leads is the server's to say.
  - Ending the journey at the first failure.

## D75: Using an element of the map

*2026-10-28, step 4, session 4c*

Measured on the captures of sessions 3g and 3h, then on a client of 3.7.12.12 in game through the host, the player's
hands off, a measuring plugin, never committed, sending uses from chosen cells ([client.md](client.md#the-game-used)):

- **Where the client stands.** On a walkable neighbour of the element's cell, one of the eight, the first its walk
  reaches, never on the element's cell even when it can be walked on; where no neighbour can be walked on, as by water,
  on one of the walkable cells nearest the element. It asks 1 to 5 ms after the server answered its confirmation.
- **What the server takes.** A use from no further than the skill's `range` (D29), counting the sum of the coordinates'
  differences (`MapGeometry.Distance`), or from a neighbour of the element's cell or the cell itself: fishing, range 10,
  was taken at 10 cells and refused at 11, three times, one of them only 6 cells off along either coordinate; the door,
  range 1, was taken from its own cell and from its west neighbour, 2 away, and refused from 2 and 3 away off its
  neighbours.
- **Line of sight does not count**: a fishing use 4 cells away, through a cell that lets no sight through on the
  straight line between them, was taken. It stays at step 9.
- **The map's elements are the map's own.** The map's information also lists the neighbouring maps' elements, marked by
  a field the references name `on_current_map`: in the maps' information of the 11 captures before this session, 104 of
  343 interactive elements and 92 of 232 stated ones, each of the 92 on another cell than the pack's, every other on the
  pack's. The server answered nothing to a use of one, three times, from next to either cell; nor to one of an element
  the map lacks. This was D60's unexplained silence.
- **Refusals**, named `InteractiveUseErrorEvent` and pinned (D54): 28 ms after the request, nothing else; two for a use
  from too far, the first naming the skill's id where the second names the instance's, five times; one for a skill the
  element does not offer now, as a resource harvested, or an instance it does not know.
- **A harvest** ends its duration later, in tenths of a second, to the millisecond, with the objects, the weight, the
  profession's experience and the element's new state within 2 ms, in no set order; the client sends nothing, and walked
  on 5 to 6 ms after. A walk sent during a harvest is refused, naming the character's cell, and the harvest goes on. A
  door's use has no duration: the new map comes in the same millisecond (D76).

So:

- **The state** keeps the map's own elements (`CharacterState.Elements`, `MapElement`): which interactive each is, its
  skills offered now and not, its cell and state, from the map's information and each update of an element it holds;
  and the use the character is in (`Use`), from the server's start of a use that lasts to its end. Walking, changing
  maps and using answer `Busy` during one.
- **Where to stand** (`ElementCells`, in `D3B.Engine.Maps`): the client's cells, the walkable neighbours of the
  element's cell in the character's zone, else the zone's walkable cells nearest it; the quickest of them is walked to.
- **The behavior** (`ElementUse`, in `D3B.Bot.Interactives`): from the state, the element's skill and the id the server
  gave it there, nothing sent when the map does not offer it; the walk (D69); the skill checked again, since another
  actor may have started harvesting; then `InteractiveUseRequest`, the start or the refusal within 3 s, and the end
  within the duration and 2 s. Outcomes (`UseOutcome`): used, busy, unavailable, no path, not walked, refused, no
  answer, not ended. The objects come as the state takes them in, which may be just after the end.
- **Rejected.**
  - Standing as far as the skill's range allows: fishing from 10 cells is no more use than the client's cell, and it
    would need the game's tables in the bots for the range, for nothing measured.
  - Keeping the neighbouring maps' elements in the state, marked: nothing can be done with them from here, and their
    stated cells are another map's.
  - Waiting for the element's new state to end a harvest, so that the objects are in: the end, the objects and the state
    come in no set order.
  - Checking the line of sight before a use: measured not to count.

## D76: Doors in the path through the world, their criteria answered strictly

*2026-10-28, step 4, session 4c; the criteria chosen by the owner. Extends D71 and D73.*

Measured on the world graph of 3.7.12.12 and the host's session of D75:

- **Doors.** 5,237 of the graph's 5,506 interactive transitions use skill 184, "Utiliser"; the 269 others use 49 skills,
  teleporters and the like, and open 42 more vertices. Every door's element stands on a cell of its map in the pack,
  with 1 to 8 walkable neighbours in the transition's zone; 50 transitions name another cell than the element's, mostly
  one that cannot be walked on. The server starts a door's use with no duration and names the new map in the same
  millisecond; the client asks what is on it 545 to 627 ms later.
- **Reached.** From the bank of Astrub, a subscribed character of level 200 holding nothing reaches 4,021 vertices
  without doors and 5,607 with them, strictly; 533 without a subscription. 7 of the 10 banks are reached, each with a
  door into it whose criterion the bot answers; quests close the doors of three other buildings at the same coordinates.
- **What the unknown codes hold back**, each family counted as met: achievements, `OA`, 1,266 more vertices; `Sc` and
  `SC`, whose meaning is not known, 550; quests, `Qa`, `Qf` and `Qo`, 248; others 41 or fewer; kamas, `PK`, and the
  breed, `PG`, none. Every unknown code met: 8,283.

So:

- **The path** follows doors, the interactive transitions of skill 184 (`WorldPathfinder.DoorSkillId`), criteria
  answered strictly as before (D71); the other interactive transitions stay out.
- **Leaving by a door** (`WorldPathfinder.ExitCells`, `MapChange`): from the cells the element is used from (D75),
  around its cell in the pack, a criterion on the cell answered for each; the walk, then the use of the skill the server
  offers on the element, then the new map as after a map action (D73). Nothing is sent when the server offers no
  door's skill on it; the refusal is `Refused`, which leaves the transition out of the journey (D74).
- **No new fact in 4c.** The bot answers the codes it answered (D71). Q12 keeps the measurements, for the large effort
  on criteria the owner noted.
- **Measured through the host**: a journey of four changes into the bank of Astrub, its door among them, in 11 s, and
  out by its map action.
- **Benchmark** (D71's): with doors, a path 3 changes long in 2.9 µs, the farthest map in 370 µs, a map the search
  cannot reach in 369 µs, 100 to 135 KB a search: twice D71's for the farthest, as the search explores more. A journey
  still finds its path again after every change (D74).
- **Rejected.**
  - Quests or achievements read in 4c: new messages to measure and pin, and the meaning of each code, for vertices the
    first milestone does not need. The owner chose to keep the doors strict.
  - Every interactive transition: teleporters and the like, not measured, for 42 vertices.

## D77: The bot sends nothing until the client drew the map

*2026-10-28, step 4, session 4c. Supersedes in part D73: a map change is over once the character is placed.*

- **Measured.** A door used as soon as the server named the new map, before the client asked what is on it: the server
  took it, and the client asked for both maps' information in turn, the server answering the second only. The bot's next
  walk, 55 s later, was never confirmed, though the server walked it; the player's next path was refused, naming that
  walk's end, and the client walked again from there. With D73's walks shown 1 and 50 ms after a map's
  information, never confirmed, a request the client must follow goes only once it has drawn the map.
- **So** `BotContext.SendAsync` holds any request until half a second after the server last placed the character
  (`BotActor.DrawDelay`), and `MapChange` no longer waits itself: the next request does. Behaviors send nothing before
  the character is placed: where it stands is not known until then.
- **Measured through the host**: the journey's walks went 533 to 549 ms after each new map's information, every one
  confirmed.
- **Rejected.**
  - The delay in each behavior after a map change, as D73 had it: a behavior started after the player's own map change
    would not wait.

## D78: Mapping format 4

*2026-10-28, step 4, session 4c*

The readable protocol gains `InteractiveUseErrorEvent`, pinned for 3.7.12.12 (D54), and `on_current_map` on the map's
elements (D75): 109 messages and 224 fields. `MappingFile.FormatVersion` is 4, `mapping-4.binpb`, 7,704 bytes, audited
without failure; the 13 game connections captured decode through it, 6,483 frames, none misread.

## D79: A bank's inside is found in play, and remembered by the host

*2026-10-28, step 4, session 4d, chosen by the owner*

Measured on the world graph of 3.7.12.12 ([client.md](client.md#banks)): from the map in front of each of the 10 banks,
doors lead to 1 to 5 maps; 16 lie at the bank's coordinates on no world map, its insides. 7 banks have one a door
without criterion leads into, 3 have two, of which only the server says which holds the banker, by the NPCs it places
there (D29). At Astrub, one door element leads to the bank and to another inside a quest closes.

- **The insides** (`WorldInsides`, in `D3B.Engine.World`): the maps a map's doors, skill 184, lead into at its
  coordinates on no world map, whatever their criteria, which the path answers strictly (D76).
- **The banks** (`BankDirectory`, in `D3B.Bot.Banks`): one per host, which every bot shares, built when a bot first
  banks from the pack's banks and bankers (D29) and the world. A bot that enters an inside tells whether a banker stands
  there; one where none stands is sought by no bot of the host, until one is seen there. Nothing is kept from one run of
  the host to the next.
- **The journey** (`Banking.GoToBankerAsync`): done where a banker stands; else to the nearest inside still sought
  (D74), then again from there, at most four journeys.
- **Rejected.**
  - Each build's insides pinned in a committed file, measured by visiting them: 10 banks to visit at every update, 3 the
    bot does not reach yet.
  - What the bot learns kept on disk: storage comes at step 5, and one more visit per ambiguous bank and per run costs a
    door and a map action.
  - The insides in the pack: the server still says which holds the banker, and the graph gives them at run time.

## D80: Banking

*2026-10-28, step 4, session 4d*

Measured on the captures of sessions 3g and 3h, then on a client of 3.7.12.12 in game through the host, the player's
hands off, a measuring plugin, never committed, sending the requests one by one, then the final behavior
([client.md](client.md#the-game-banked)):

- **Talking.** NPC action 3 on the banker, naming the map and his actor id, from 10 cells away: the dialog and its
  question in the same millisecond, 28 ms later, with two replies, one of which the pack lists among his. The bank of
  Astrub holds two bankers, NPCs 520 and 522.
- **Opening.** That reply ends the dialog and opens the exchange with the storage, its content and the character's kamas
  in the same millisecond, the opening paid with as many kamas as the storage holds stacks.
- **Putting in.** One request lists the stacks: the server takes those it may, answering with the inventory's stacks
  deleted, the storage's and the weight, and leaves the others, such as a quest's tickets, answering nothing when it
  takes none. Kamas by another request: the storage's, then the character's.
- **Refused**: an action on an NPC the map lacks, or during a dialog or an exchange, with an empty event, named
  `NpcActionErrorEvent` and pinned (D54); a reply not offered, with the question again and a text; nothing at all for a
  request naming another map, a reply, a stack or kamas outside the exchange, no kamas, more than the side holds, or a
  stack the inventory lacks. An action the banker does not offer opens an exchange that fails, with an event the bot
  never meets, not named.
- **Busy.** The server refuses a walk during the dialog and during the exchange, naming the character's cell, and the
  dialog or exchange goes on.

So:

- **The state** keeps the map's NPCs (`CharacterState.Npcs`), the dialog with one (`Dialog`, its question and replies)
  and the exchange with the storage (`Storage`, its kamas), from the server's events. The storage's objects are not
  kept: what the server sends as stacks are taken out is not measured. Walking, using, changing maps and travelling
  answer `Busy` during a dialog or an exchange.
- **The behavior** (`Banking`, in `D3B.Bot.Banks`): to a banker (D79); the action, then the question, 3 s; the banker's
  reply the question offers, then the storage's content, 3 s, a dialog it could not take to the bank left; the stacks
  asked for that the character carries and does not wear in one request, then the kamas, at most those carried, each 3
  s; then leaving, 3 s, the bank left whatever the deposit gave. Every request waits until the client drew the map
  (D77). Outcomes (`BankOutcome`): done, busy, no bank, not travelled, refused, no answer.
- **Measured through the host**: from inside the bank, two stacks and one kama put in and the bank left in 161 ms; from
  two maps away, the journey through the door, the bank opened 529 ms after the inside's information, a kama put in and
  the bank left, in 3.9 s.
- **Rejected.**
  - One request per stack, as the player drags them: the server takes a list, measured.
  - Choosing what to put in, such as the kamas kept for the next opening: the caller's, as step 6's profiles will be.
  - Walking next to the banker first: the server took the action from 10 cells away.

## D81: The player's leaving is told from the client's by the dialog

*2026-10-28, step 4, session 4d. Supersedes in part D66: leaving a dialog is now the player's while the character is in
one.*

- **Measured.** The player closing the banker's dialog or the bank: the client's request to leave, while the dialog or
  the exchange is open, then the server's end 28 to 60 ms later. D3B leaving the bank: the server's end, then the
  client's two requests by itself, 1 to 178 ms later, six times; none once, after a bank D3B opened and left in 161 ms.
  The relay shows each frame of the server to the observers before the client gets it, so a bot's loop sees the end
  before the client's requests.
- **So** `PlayerActions.Starts` takes the state: a request to leave is the player's while the character is in a dialog
  or an exchange, and the client's own once the server ended it. Measured through the host: the player closing the bank
  the bot had opened interrupted the bot, which paused.
- **Rejected.**
  - A delay after the server's end within which a request to leave is the client's: the state already says it.

## D82: Mapping format 5

*2026-10-28, step 4, session 4d*

The readable protocol gains `NpcActionErrorEvent`, pinned for 3.7.12.12 (D54), and names the dialog and exchange values
measured on the bank: 110 messages and 224 fields. `MappingFile.FormatVersion` is 5, `mapping-5.binpb`, 7,743 bytes, the
same bytes on two runs, audited without failure; the 15 game connections captured decode through it, 8,851 frames, none
misread. The release of 3.7.12.12 needs it, which the release tool publishes (D50).

## D83: The path through the world teleports from where the character stands

*2026-10-28, step 4, session 4e, chosen by the owner. Extends D71, D73 and D74.*

Measured on a game connection of the player using zaaps, the haven bag and the map, then on a client of 3.7.12.12 in
game through the host, the player's hands off, a measuring plugin, never committed, sending the requests one by one
([client.md](client.md#the-game-teleported)):

- **Three ways to one request.** Using a zaap, skill 114, lists the zaaps the character knows with their costs; using
  the haven bag's zaap lists them too; and the client, when the player chooses a zaap on its map, teleports with no list
  open, the teleporter's type 5. Each teleport is one request, answered 28 to 33 ms later, once 96, with the new map,
  the kamas paid and the dialog over; the client asks what is on the map 0.66 to 1.36 s later, after D3B's teleports as
  after the player's.
- **From where.** Teleporting from the map was taken from 6 maps whose flags carry bit 3, a bank's inside and maps
  without a zaap among them, and refused from one without it and from the haven bag; 10,587 of the graph's 11,327
  vertices carry it, every zaap's map among them. The haven bag opens where bit 2 is set, 3 maps measured, refused on 2
  without it; 136 vertices carry bit 2 and not bit 3.
- **What it costs.** Ten kamas a whole coordinate of the straight line between the map the character stands on and the
  zaap's, and a thousand more towards Incarnam's world map: the 120 costs of four lists and the 13 teleports paid. The
  haven bag's list costs from the map the bag was entered from, so it saves nothing. Bubble's formula, ten kamas a tenth
  of a coordinate, gives costs that are not tens, and the server's are.
- **Known.** The server lists the zaaps the character knows on entering the game, and again, whole, when it enters the
  map of one it did not know; a teleport to any other map is refused.

So:

- **The path** (`WorldPathfinder`, owner's choice): from every vertex whose map carries bit 3
  (`MapPosition.AllowsTeleport`), a teleport to the zaap of every map the character knows one on
  (`WorldPathRules.KnownZaaps`) whose cost it carries (`WorldPathfinder.TeleportCost`, `WorldPathRules.Kamas`), one map
  change like any other (D71); a step that teleports has no transition (`WorldStep.IsTeleport`). It lands on the zaap's
  map's only vertex: the one zaap's map with two zones, the Craqueleurs mountain's, is left out, where the character
  lands there not measured. Only a subscribed account teleports, prudently: no account without a subscription was
  measured (D72). The kamas towards other world maps than Incarnam's are not measured, and counted as Incarnam's.
- **The behavior** (`MapChange`): `TeleportRequest` from the map, then the new map and what is on it, as after a scroll,
  every request waiting for the client to draw the map (D73, D77); an error text refuses it (D84), and a refused or
  unanswered teleport leaves every teleport out of the rest of the journey (`WorldTravel`, D74).
- **The state** keeps the zaaps the character knows (`CharacterState.KnownZaaps`), the zaap's dialog with its costs
  (`ZaapDialog`), which leaving ends and during which walking, using, changing maps and travelling answer `Busy`, and
  the haven bag the character is in, by its owner (`HavenBagOwnerId`). A teleport or the haven bag's request the player
  sends is the player's (D66).
- **The haven bag**: a journey from the character's own leaves it first, back to where it was entered, free
  (`MapChange.LeaveHavenBagAsync`); from another's, which is not measured, it finds no path. The path does not travel
  through it.
- **Measured through the host**, the player's hands off: from an inside of Amakna's castle, which no one teleports from,
  to Astrub, out by its door, a teleport to Astrub's zaap and a scroll, 3 changes in 6.1 s; from the haven bag to
  Sufokia's zaap, the bag left and a teleport, in 2.9 s; from there to the nearest banker, a teleport to the Cœur
  immaculé's zaap, a scroll and a door, in 6.6 s, every walk confirmed and every cost the one the rule gives.
- **Benchmark** (D71's, `benchmarks/D3B.Engine.Benchmarks`): every zaap known and the kamas carried, the farthest map in
  591 µs and a map the search cannot reach in 638 µs, about 190 KB a search, against 266 and 281 µs on foot, as
  teleports open more of the graph. A zaap the search reached once leaves its list: every teleport costing the same, no
  later vertex brings it nearer; looking at every zaap from every vertex took 1.45 ms. A journey still finds its path
  again after every change (D74).
- **Rejected.**
  - Walking to a zaap and using it: two to three steps more for the same destinations and costs.
  - The haven bag and its zaap: two steps a teleport, for 136 vertices more.
  - All three: much more code and tests, for 1.2% of the graph.
  - Weighing kamas against map changes: nothing measured sets the rate; step 6's profiles may.
  - Asking the server what a teleport costs first, as the client does before the player's: a request that starts no
    action (D60), whose answer the measured rule gives.

## D84: The teleports' messages named, their refusals texts: mapping format 6

*2026-10-28, step 4, session 4e*

- **Named and pinned** for 3.7.12.12 (D54), each among the messages its side carried once the player teleported:
  `ZaapKnownListEvent`, `TeleportDestinationsEvent`, partial, `TeleportRequest`, `HavenBagEnterRequest` and
  `TextInformationEvent`, partial; with them, the teleporter's type (`TeleporterType`: a map's zaap 0, the haven bag's
  3, from the map 5), the haven bag in the map's information (`HavenBagInformation`, a oneof's member the other maps
  leave unset), and the dialog's type 10, a zaap's (`DIALOG_TYPE_TELEPORT`).
- **Refusals.** The server refuses a teleport with a text of the error kind, 28 ms after the request, and nothing else:
  474 during a dialog or a walk the client did not confirm, 1759 to a map the character knows no zaap on, 416 from a map
  no one teleports from or from the haven bag; 471 the haven bag where it does not open. A teleport of type 0 with no
  list open gets no answer. The bot reads the kind, not the text.
- **Not named**: the client's question of what a teleport costs, before the player's from the map, and its answer: the
  bot does not ask (D83).
- **Mapping.** The readable protocol holds 117 messages, 241 fields, 19 enums and 147 values.
  `MappingFile.FormatVersion` is 6, `mapping-6.binpb`, 8,304 bytes, the same bytes on two runs, audited without failure;
  the 18 game connections captured decode through it, 11,504 frames, none misread. The release of 3.7.12.12 needs it,
  which the release tool publishes (D50).

## D85: A walk the client does not follow leaves the character where the server has it

*2026-10-28, step 4, session 4e, chosen by the owner. Supersedes in part D69: a walk not confirmed now ends with the
character on its last cell. Extended by D89: the server may still hold that walk, which a step aside lets go.*

- **Measured** ([client.md](client.md#the-game-teleported)): of the 104 walks D3B sent in the 18 captures, the client
  confirmed 97, the player stopped one, and the client never confirmed 6, which the server walked: two shown 30 and 90
  ms after a map's information (D73), one after a door used too soon (D77), two in the bank of Astrub 81 and 301 s after
  its information (4d), and one in 4e, 0.53 s after the information of a map the client had never drawn, along 14 plain
  cells. The server then answered a teleport and the haven bag as busy, 9 and 11 s later, and in 4d refused the player's
  path, naming the walk's last cell. A walk sent from that cell 5 minutes later was walked, and the client first walked
  its character there from where it had kept it, then confirmed. The cause is not found: of the walks sent 0.5 to 0.6 s
  after a map's information, the client confirmed 36 of 37.
- **So** `MapWalk`, once the client did not confirm in time, has the bot's loop take the walk's last cell
  (`BotContext.SettleWalkAsync`, `CharacterEvents.NotConfirmed`); the outcome stays `NotConfirmed`, and a change or a
  journey goes on from there, leaving out what failed (D74), instead of answering `Busy` until the server's next
  refusal.
- **Risk.** The server may still hold the character walking, and refuse the next request once.
- **Rejected.**
  - A longer delay before the first request on a map: nothing measured says it prevents this, and two came minutes
    after.
  - Leaving it to session 4f: a journey stopped at each one, the owner's choice.

## D86: Floors count on a map with stairs

*2026-10-28, step 4, session 4f. Supersedes in part D68: a step's floors may forbid it.*

Measured on the pack of 3.7.12.12, the 18 game connections captured before this session, then on a client of
3.7.12.12 in game through the host, the player's hands off, a measuring plugin, never committed, sending chosen paths
([client.md](client.md#the-game-interrupted)):

- **The pack.** A cell's `moveZone` is 0 or 1, 1 on 9,800 cells; 633 maps hold both, 469 of them in the world graph, and
  their cells of moveZone 1 are stairs. Every walkable cell's floor is a multiple of 10.
- **The captures.** On maps of one moveZone, the player walked steps of 10 to 60. On maps of both, the player's client
  and others' walked steps of 30 along a stair, and of 10 between a stair and a cell of moveZone 0. The path refused in
  session 4e stepped from 70 on a stair to 0 off it.
- **The server**, 14 paths each ending on the step tried, every refused one walked again without that step: on a map of
  both moveZones, along a stair, 130 both ways walked; between moveZones 0 and 1, 10 walked, 20, 30, 40 both ways, 70
  and 130 refused; between two cells of moveZone 0, 10 walked, 20, 40 and 130 refused. On a map of one moveZone, 170
  walked. The client confirmed every path the server walked.

So:

- **The rule** (`CellPathfinder`): on a map whose cells hold both moveZones, a step between two cells that are not both
  of a stair, moveZone 1, changes floor by 10 at most; elsewhere floors do not count. The difference is the floors'
  own: Dofus 2's, of their absolute values, gives the same on every pair of the pack. It leaves out 5,655 pairs of
  walkable neighbours, on those 633 maps.
- **Tests**: the steps of each kind, and the property tests on random maps of floors 0 to 30, half with stairs, against
  an exhaustive search applying the rule.
- **Measured through the host**: the journey off the upper level of Sufokia's zaap's map, which the old paths failed
  after three refusals, in 5.3 s, no refusal; then 85 journeys of an activity between that map and the one below its
  stairs, every one arrived.
- **Rejected.**
  - BillBot's rule, Dofus 2's: no change of floor at all between moveZones. The client and the server take 10.
  - Weighing a change of floor in a step's cost: nothing measured says it takes longer.

## D87: Activities, steps chosen from the state

*2026-10-28, step 4, session 4f, chosen by the owner, who asked for a model both permissive and practical, that holds
for a master and its followers. Builds D9.*

The owner's profession scripts in `vendor/metiers` ask the bot for the same: their `move()` is called on every map and
chooses there, harvesting, changing maps, or `bank()` once `inventory:podsP()` passes their `PODS_THRESHOLD`; they
know mules. BillBot's behaviors, read for ideas (D3), resume by deciding again from the game's state with steps that
can run twice, check their stop conditions at safe points, and pass a gate before acting; they also give up a wait and
carry on as if it had worked, treat a reconnection as the fix for any desync, and share global state between bots, which
D3B does not take. BillBot has no master and followers.

- **An activity** (`BotActivity`, in `D3B.Bot.Activities`), written by a module or a plugin (D90): `StepAsync`, a
  behavior's worth of work chosen from the state when the step starts, saying whether the activity goes on. Every other
  hook has a default, so that an activity overrides what it needs: `IsFull`, never, and `UnloadAsync`, unable to;
  `OnInterrupted`, resume; `RecoverAsync`, the default recovery (D89).
- **Its run** (`ActivityRun`, `BotActor.StartActivity`), one per bot: each step, and each unloading and recovery, a
  behavior the bot starts once it can act, a game connection playing the character, placed on a map and out of a fight,
  the player not having taken it over (`RunWhenReadyAsync`). Until then it waits, with no delay. Before each step, a
  full character is unloaded instead, and a character still full once unloaded fails the activity. An interruption
  cancels the step (D9); the activity resumes, or stops, as `OnInterrupted` says; the host stopping ends it, as
  `Stop` does. It says what it does (`ActivityStatus`: waiting, stepping, unloading, recovering, over), its steps, its
  last interruption, and how it ended (`ActivityOutcome`: done, failed, stopped).
- **No position kept.** The next step starts from the state, so the activity resumes where the state allows: after a
  pause, a new connection or a fight, as after a journey that went elsewhere.
- **For groups**, at step 10: anyone may wait for what a bot's state becomes (`BotActor.WaitUntilAsync`), tried by its
  loop now and after everything it handles, as a follower's step will wait for its master's map; a behavior waits on
  its own state with a delay (`BotContext.WaitUntilAsync`).
- **The name**: `BotActivity`, not `Activity`, which `System.Diagnostics` holds, as D67 named the actor.
- **Measured through the host**: a patrol between two maps, 85 journeys; the player's walk interrupted it and paused the
  bot, resumed 22 s later it recovered and went on; full, it went to the nearest bank, put a kama in and left, in
  6.6 s, then went on; the client closed during a journey, the new connection 20 s later took it back, and it went on
  2.2 s after the server named the map, with no command.
- **Rejected.**
  - An activity as one long behavior, started again after each interruption: the run time would see nothing between
    steps, full pods left to every activity, and no step for step 5's app to show.
  - A state machine or a tree of behaviors, with flags: what D9 left out.
  - A position the activity saves to resume from: the server's state is what holds.
  - A delay after which a waiting activity gives up: the client or the player may come back much later.

## D88: A fight is an interruption, until step 9 fights it

*2026-10-28, step 4, session 4f*

Measured on a fight in a capture of session 4d and one through the host, the player attacking monsters
([client.md](client.md#the-game-interrupted)):

- **The start.** The client walks to the group, then sends its confirmation and a request the readable protocol lacks,
  the attack, in one read. The server answers the confirmation, replaces the roleplay's context with the context of
  value 1, and names the fight's own map, a map of the pack off the world graph, at 0,0. It sends no map information:
  the client's request for one gets the fight's.
- **During it**, the client moves the character with `MapMovementRequest`, on the fight's map.
- **The end**: the loot, the context of value 0 again, the map the fight began on, then its information, placing the
  character.

So:

- The value is named `GAME_CONTEXT_FIGHT`. Value names stay out of the readable protocol's digest (D45): mapping 6's
  pairs are the same, only the tool's version differing on a new run, so the format stays 6 and the release keeps its
  mapping.
- **The state** says the character fights (`CharacterState.IsFighting`), and `IsBusy` gathers what makes every behavior
  answer `Busy`: a walk, a use, a dialog or exchange, a fight.
- **The bot** interrupts its behavior once the server creates a fight's context (`Interruption.Fight`), and pauses
  nothing; an activity waits for the fight's end and its map's information, then recovers and goes on.
- The player attacking a group walks first, which pauses the bot (D66) before the fight.
- **Not measured**: a fight lost, and a fight beginning during a step, as an aggression would: the tests cover the
  second. Step 9 fights; its strategies will be what an activity runs on this interruption.
- **Rejected.**
  - Pausing the bot on a fight, as on the player: the owner's model resumes once the fight is over.

## D89: Recovery, the client's new connection, and a walk the server holds

*2026-10-28, step 4, session 4f. Extends D85.*

Measured through the host ([client.md](client.md#the-game-interrupted)):

- **A disconnection.** The client closed 1.7 s into a walk it had not confirmed: on its new connection, 22 s later, the
  server placed the character on the walk's first cell, and walked the next path at once. During an activity's journey,
  the new connection came 20 s later, and the activity went on as above (D87).
- **A walk the server cuts.** On one map, a scroll's walk to the edge passes a map action's cell, where the server stops
  it: of 43 such walks, the client followed 24 at once, confirming them 0.43 to 0.44 s later, and 18 only once D3B had
  sent another walk, the server then refusing that one by the first cell of the cut walk, not its last, and the
  client's confirmation setting off the map action. Both kinds were sent 0.50 to 0.51 s after the map's information.
  The journey arrived every time, each such walk costing about 3.5 s. This is likely D85's walk the client does not
  follow; its cause is not found.
- **A request naming no cell**, measured once: refused, then the server closed the game connection, 83 ms after.

So:

- **The recovery** (`Recovery.SettleAsync`, every activity's default): a use goes on to its end, 30 s at most; a walk
  to its confirmation, else it settles where the server has the character (D85); a dialog or an exchange still open is
  left; a walk the server holds is let go of. Each wait is bounded, and the next step finds the state as it is.
- **A walk the server holds** (`CharacterState.IsWalkHeld`): set once a walk the client did not confirm settled (D85),
  cleared by the next walk, refusal or map. The server refuses what is not a walk from that cell, measured in 4b, 4d and
  4e, and walks one from there. So `MapWalk.ReleaseHeldWalkAsync` steps to a neighbour first, before a map change, a
  teleport, leaving the haven bag, using an element or talking to the banker, and a walk to the cell the character
  stands on steps aside and back.
- **No walk of no step**: `MapWalk.Request` refuses a path that does not move, which the server answers by closing the
  connection.
- **Risk.** A walk the client does not follow is still not understood: the state may take the wrong cell until the next
  refusal, as on the map action's cell, and a journey loses seconds. The step aside rests on one measured fact, a walk
  from the held walk's cell walked; no held walk was provoked to test it.
- **Rejected.**
  - D3B reconnecting the client: the client logs in by itself (D23).
  - A longer delay before the first walk on a map: the walks followed and those not were sent within the same 10 ms.

## D90: Plugins, not Lua

*2026-10-28, step 4, session 4f, chosen by the owner. Supersedes D12; drops Q1 and step 7. Superseded by D102: Lua
scripts replace the plugins.*

The owner does not want Lua scripts: routes and routines are written as .NET plugins, like every feature (D5, D6, D21),
their activities `BotActivity` (D87).

- **So** there is no Lua host and no script API: `src/D3B.Scripting`, an empty project, is removed. Q1, whether
  LuaCSharp holds up, has no object. Step 7 is dropped and keeps its number, as step 8 did. Step 13 turns the owner's
  profession scripts in `vendor/metiers` into plugins.
- **What a plugin sees of the bots** becomes what writing an activity needs: Q11, still settled before step 6
  publishes the package.
- **Rejected.**
  - Lua beside plugins, as D12 had it: a second API to keep, which the owner does not want.

## D91: The bots run in the app's process

*2026-10-29, step 5, session 5a, chosen by the owner. Supersedes D7.*

The owner asked why the app should not run the bots itself. The lead recommended keeping D7: with D3B in the middle of
every connection (D23), the process that holds the relay holds every client's game connection, so a fault of the app,
or closing its window, would disconnect them all, and the sessions writing the app restart it often. The owner chose
the app.

- **So** the Avalonia app runs the bots' host in its own process (`HostRunner`, in `D3B.App.Hosting`): it starts it
  with the app, off the UI's thread, shows where it stands and why it did not start, and stops it once the window is
  gone, the clients getting their configuration back (D35). There is no gRPC API: `D3B.Contracts`, an empty project, is
  removed, as `D3B.Scripting` was (D90).
- **The host's composition is shared** (`BotHost`, moved from `D3B.Host` to `D3B.Bot.Hosting`): the default modules then
  the plugins, the pack and the mapping got ready, the relay started, or why not (`BotHostStart`). `D3B.Host` keeps its
  entry point alone, the bots without the app, for measurements; its tests move to `D3B.Bot.Tests`, and
  `D3B.Host.Tests` goes.
- **Risk, accepted by the owner.** Closing the app, or the app failing, disconnects every client; after a crash,
  `scripts/client.cs restore` gives the client its configuration back. A remote front, which D14 kept possible, would
  need an API again.
- **Measured**: closing the app gave the client its configuration back and closed its game connection, as stopping
  `D3B.Host` does (D94).
- **Rejected.**
  - Keeping D7, the lead's recommendation: its cost was an API, about one session, then a few calls per page.
  - The app starting `D3B.Host` as a process of its own: the same separation, which the owner did not want.

## D92: Step 5 in five sessions

*2026-10-29, step 5, session 5a, chosen by the owner. Replaces the six sessions the owner chose earlier in the same
session, which began with the host's gRPC API, before D91.*

- **5a**: the app with the bots in it: Avalonia, ShadUI and CommunityToolkit.Mvvm, the host in the app's process (D91),
  French and English from the start, the bots page, and the catalogue it starts activities from (D93, D94).
- **5b**: what a plugin sees and declares: Q11, and Q6, which D91 makes simple: a plugin's page loads in the app's
  process, registered by its module as D5 planned. The sample plugin writes an activity and a page.
- **5c**: accounts and profiles in SQLite, EF Core and its migrations, and their pages. D3B sees no password since D23:
  whether Data Protection has a secret to protect is decided there.
- **5d**: the live map, read from the pack by the app.
- **5e**: logs and the protocol inspector: the logs kept in memory, and a bot's game frames decoded, live with the
  ticket hidden (D65), or replayed from a capture (D63).
- **Rejected.**
  - Q11 at the start of step 6: 5c's profiles depend on how a plugin's activity declares its settings.

## D93: Activities the app starts, from a catalogue the modules fill

*2026-10-29, step 5, session 5a. Builds D87. Its point on plugins is superseded by D95: they reach `D3B.Bot`, and
register activities.*

- A module registers an activity with `services.AddActivity<T>(id, name)` (`ActivityServiceCollectionExtensions`, in
  `D3B.Bot.Activities`): an `ActivityDefinition`, its id unique in the host, its name as the app shows it, and how one
  is made: a new one for each start, its constructor given the host's services, since an activity runs once.
- The bots module registers the `ActivityCatalog` of them, in the order they were registered. An id registered twice
  fails while the modules build, so a plugin reusing one stops the host, as an invalid plugin does (D21).
- The app starts the activity chosen on a bot (`BotActor.StartActivity`), stops it (`ActivityRun.Stop`), and resumes
  the bot the player took over (`BotActor.Resume`, D66). An activity started by other code shows by its own name.
- Plugins cannot reach `D3B.Bot` yet (D22): until Q11, in session 5b, D3B's modules and a measuring plugin compiled
  against the projects register activities. D3B writes its own from step 6.
- **Rejected.**
  - Activities found by reflection over the loaded assemblies: a module says what it brings, in code (D21).
  - An activity's settings now: they come with what a plugin declares (5b) and with profiles (5c).

## D94: The app's first version

*2026-10-29, step 5, session 5a; dark by default, chosen by the owner, who asked for a real effort on the interface,
then for a page more practical and pleasant than three panes, its design left to the lead. Builds D14. The theme and the
language are remembered since D101.*

- **The stack** (D15): Avalonia 12.1.4, ShadUI 0.2.4, CommunityToolkit.Mvvm 8.4.2, and Avalonia.Fonts.Inter 12.1.4,
  Inter being the typeface ShadUI's design comes from, the same on every machine. Avalonia's packages ShadUI brings are
  pinned at 12.1.4, ShadUI asking for 12.1.0. `D3B.App` targets `net10.0` (D19).
- **The window**: dark, light on a click, which nothing remembers before 5c's storage. No sidebar while there is one
  page: where the host stands is in the title bar, beside the language and the theme. While the host gets ready, a
  spinner; when it did not start, why, in a block the owner can copy, since it often names a command to run; then the
  bots page.
- **The bots page**: a card per bot, in the order they came, in as many equal columns as the window's width holds
  (`CardGrid`), so that every bot shows whole, its actions on it, nothing to select. A card says who: initials with a
  dot of the bot's status, name, level, connection; where, the coordinates in large figures, as players give a place,
  the sub-area named in the app's language from the pack's texts and the cell, the map's id in a tooltip; what it
  carries, pods with a bar that turns to the warning tone past 90 %, a hint only, each activity deciding when it
  unloads, and kamas; what it is in the middle of. At its foot, the activity: the player's pause with the button that
  resumes it, what the activity does or how it ended, its steps and last interruption, the button that stops it, or the
  list to choose one from and the button that starts it. Figures are tabular. Every status has a tone (`Tone`):
  success, warning, info, danger or neutral. The icons are the app's own line drawings (`Icon`).
- **Reading the bots**: the window reads every bot's snapshot each 250 ms, on the UI's thread (D9). The bots' loops
  never wait for the app, and raise nothing to it.
- **Translation**: resources in English (`Strings.resx`) and French (`Strings.fr.resx`), the same keys, which a test
  checks. The language starts as the system's and changes live: views bind through `{l:Tr Key}`, which follows it
  because `Localizer` raises the indexer's own name, `Item`, the only one Avalonia 12.1.4's bindings to an indexer
  listen for, not WPF's `Item[]`: the test drawing the window found it. A text that counts takes the singular or the
  plural as its language has it, French's 0 singular. The host's own messages, such as why it did not start, stay in
  English.
- **Tests**: the view models against bots played as `D3B.Bot.Tests` plays them (D67), the runner against a test
  install, and the window drawn by Avalonia.Headless with Skia, its texts read in both languages.
- **Measured** on a client of 3.7.12.12 through the app, with a measuring plugin, never committed, whose patrol goes
  between the character's map and a neighbour ([client.md](client.md#the-game-from-the-app)): the bot showed once the
  character was chosen; started from the app, 12 journeys arrived; the player's walk paused it twice, resumed from the
  app it went on; stopped from the app; closed, the app gave the client its configuration back.
- **Rejected.**
  - Three panes, a sidebar of pages, the list of bots and the one chosen in full, the first layout shown: the owner
    found it neither practical nor pleasant.
  - A table, a row per bot: denser, but a row cannot hold the pause and the activity's controls without crowding.
  - A tab per bot, as the bots sold do: one bot at a time, when the owner runs several.
  - The window told of each change of a bot: a handler on the bots' loops and a dispatch per message, for a page read
    four times a second.
  - Changing the language through a restart: the bots live in the app now (D91), and a restart disconnects them.
  - An icon font, as ShadUI's demo uses Lucide's: a binary asset and its notice, for a dozen icons.

## D95: Plugins compile against the D3B.Bot package

*2026-10-29, step 5, session 5b, chosen by the owner. Settles Q11. Supersedes D22 in part: D3B.Core is no longer the
only package, nor what a plugin references. Superseded by D102: there are no plugins to compile.*

What a plugin needs to write an activity is the bots' run time itself: `BotContext` names the pack's maps
(`D3B.Data`), the paths through the world (`D3B.Engine`), the side a message came from (`D3B.Network`) and the readable
requests (`D3B.Protocol`); the state is built from the protocol's messages; and the behaviors an activity is made of,
walking, travelling, using an element, banking, live beside the actor.

- **So** `D3B.Bot` and the four libraries its API names are packable beside `D3B.Core`, at the same MinVer version:
  `dotnet pack -o <feed>` at the repository's root packs the six, and nothing else. A plugin references `D3B.Bot` with
  `ExcludeAssets="runtime"`, which brings the five others and their packages, runtime excluded too. Measured: a plugin
  built from the template holds only its own files and its French satellite assembly; `D3B.Network`'s framework
  reference to ASP.NET Core reaches its `runtimeconfig.json`, harmless, the host running it.
- **What a plugin sees** is the public API of those six projects: the state (`CharacterState`), activities
  (`BotActivity`) and their context, the behaviors (`WalkAsync`, `TravelAsync`, `UseAsync`, `BankAsync`, ...), the
  catalogue of D93, the bots (`Bots`, `BotActor`) and the game's observers, the pack's data, and the pages of D96. A
  plugin's activity gets the host's services through its constructor (D93).
- **In the repository**, the sample references the project `D3B.Bot` the same way, with every project reference
  `Private` false, so that the projects `D3B.Bot` references do not land in its folder either.
- **The template** references `D3B.Bot`; CI packs the six, then builds a plugin from it with a page, whose folder must
  hold its own files alone, and one without.
- **Risk.** Everything public in the six projects is what a plugin sees, about 170 types written by hand and the
  protocol's generated classes, and it changes every session. Before the first milestone publishes the packages (D4,
  D22), what serves only the host is to be made internal, and the API kept stable from then on.
- **Rejected.**
  - The bot's contracts moved into `D3B.Core`: they cannot be told apart from the run time; `D3B.Core` would take most
    of `D3B.Bot` and its four libraries, or hide them behind interfaces, and stop being the module model (D22).
  - A facade of interfaces, `D3B.Sdk`, implemented by `D3B.Bot`: a stable contract, but a second API kept in step with
    the first, every behavior written twice, while the API still changes every session. It stays possible once the API
    settles.

## D96: A plugin's page, in the app's rail

*2026-10-29, step 5, session 5b; the rail chosen by the owner among three mockups. Settles Q6. Builds D5, D91, D93 and
D94. The rail shows always since D101, the app having pages of its own; pages come from D3B's own modules alone since
D102.*

- **Registered by its module**, as D5 planned: `services.AddPage(id, title, icon, create)`
  (`PageServiceCollectionExtensions`, in `D3B.Bot.Pages`), a `PageDefinition`, its id unique in the host, which a
  second registration refuses while the modules build, as for activities (D93). The bots module lists them in the
  `PageCatalog`, in the order they were registered. `create` returns the page, an Avalonia control, as an `object`: the
  contract names no type of the UI's framework, so that a host without the app loads a plugin with a page (D97).
- **What a page receives**, `PageContext`: the host's services, the bots, the catalogues, the pack's data and the
  plugin's own services; the app's language, French or English, and its change; the language of the pack's texts that
  goes with it; and the window's refresh, every 250 ms while the page shows, on the UI's thread, as the app's own pages
  read the bots' snapshots (D9, D94). `Texts(resources)` gives the plugin's own texts in the app's language, which
  follow it as it changes, for the page to bind to, `{Binding Texts[Key]}` (`PageTexts`), with `Format` and `Plural`.
- **Translated names.** A text the app shows, a page's title or an activity's name, is a `LocalizedText`
  (`D3B.Bot.Localization`): the same in every language, or one of a module's resources in the language asked for. A
  plugin's resources are its own `.resx`, in English and French; the French build into a satellite assembly in the
  plugin's folder, which its load context finds through its `.deps.json` (D21), measured by the app's tests.
  `AddActivity` takes a `LocalizedText` too, and still a plain name, the same in every language.
- **In the app**: a rail on the left, 60 px wide, the bots page first, then a line, then the modules' pages in the
  catalogue's order, each an icon with its title as a tooltip, the page shown in the rest of the window. It shows once a
  module registered a page: while there is one page, there is no rail (D94). An icon is a path in the syntax of SVG on
  a grid of 24 units, drawn as the app's own icons, round lines in the colour of the text; one the app cannot read, for
  which Avalonia throws `InvalidDataException`, or none, gets the app's own.
- **A page fits the design** by what it inherits: the theme, dark or light, Inter and ShadUI's styles; and by the app's
  classes it may use, `PageHeader` and `PageTitle`, which the bots page uses too, `Plate` and `PlateFooter`, its cards',
  and the typography's.
- **A page is made the first time it shows**, with its context, and kept. One that throws as it is made, as it
  refreshes or as the language changes, or that is no control, is replaced by what it threw, each exception's type and
  message with the inner ones, in a danger block, and logged; its context raises nothing more, and the first fault stays
  shown. So is a page whose own code throws on the UI's thread what nothing caught, in a click or a layout: the app tells
  it by the page's factory's assembly in the exception's stack (`PageDefinition.Owner`), and what no page's code threw is
  still the app's fault, which ends it. The bots and the other pages go on: the app holds every client's connection
  (D91), and its fall would disconnect them all.
- **Nothing a module registered throws in the app.** A text whose resources cannot be found, such as a `.resx` moved to
  a folder, which `GetString` throws for, shows its key between brackets, as a missing one does. The catalogues are read
  before the host starts, so that two activities or two pages of one id, however registered, stop the host, which says
  so, rather than the app later. A page asking for its texts on every refresh gets the same ones each time.
- **The sample** (`samples/D3B.Samples.Hello`) writes an activity and a page: the stroll walks the character ten times
  to a cell within 4 of where it stands, of its zone, away from the cells of the map actions the world graph names,
  chosen from the state at each step (D87), and waits while the character is too busy for a walk, which would come back
  at once; a journal the activity writes and the page reads; and its page, a card per bot, whether it strolls, how many
  walks arrived, the last one, and the stroll's progress. Its texts are in English and French. Drawn without a screen,
  in both languages and both themes, and at 760 px wide.
- **The template** writes an activity and a page; `--page false` writes the activity alone, with no reference to
  Avalonia. It compiles pages against the Avalonia and CommunityToolkit.Mvvm the app runs, which CI checks.
- **Not measured**: the stroll on a client through the app; the tests play it through the sample loaded as a plugin.
- **Rejected.**
  - The pages as tabs in the title bar, the lead's recommendation: the full width left to the cards, but the owner
    chose the rail.
  - A section a plugin adds to each bot's card, alone or with the pages: the cards grow with every plugin, for no need
    a plugin has yet.
  - A contract typed with Avalonia, in a package of its own: `D3B.Host` would need Avalonia to build a plugin's modules.
  - The app's `{l:Tr Key}` for plugins' views: it reads the app's resources, inside `D3B.App`, which no plugin
    references.

## D97: The loader reads a plugin's root module from its metadata

*2026-10-29, step 5, session 5b. Builds D21. Superseded by D102: the loader goes.*

- `PluginLoader` finds the public class marked `[Plugin]` by reading the assembly's metadata
  (`System.Reflection.Metadata`), then loads that type alone. Looking for the attribute by reflection over every public
  type, as it did, loads every assembly their declarations name: a public page, deriving from Avalonia's `UserControl`,
  would stop a host without the app, `D3B.Host` or a test's, from loading the plugin. Measured by `D3B.Core`'s tests
  and `D3B.Host`'s composition in `D3B.Bot`'s, whose hosts hold no Avalonia: the sample, whose page is public, loads,
  its modules build, and its activity and page reach the catalogues, Avalonia never loaded.
- What the rest of a plugin needs then loads as its modules build: an assembly neither the host nor the plugin holds,
  told by the display name the runtime gives it rather than a file's path, is a `PluginLoadException` naming the plugin,
  and the host does not start (D21). So is a type of the plugin's own that bears the attribute's name.
- The root module is a top-level public class: a nested one, which reflection over public types found too, is not.
- **So** a plugin names its page's types in the page's factory alone, never in its module's own code, which a host
  without the app runs.
- `D3B.Core`'s tests reference `D3B.Bot`, which the sample compiles against, as a host provides it.
- **Rejected.**
  - A plugin's page types required to be internal: what a template's author writes by default breaks the host.
  - `System.Reflection.MetadataLoadContext`: a package, for a search the metadata reader in the framework does.
  - `D3B.Host` shipping Avalonia: a UI framework in the host without the app, for types it never makes.

## D98: An activity's settings, a class its module describes in code

*2026-10-29, step 5, session 5c; the description in code, and the scope with the game's values and every mechanism,
chosen by the owner, who noted that the auto modes to come, BillBot's for one, focus a profession, maps, resources of the
profession's level, a monster, an area or a sub-area. Builds D93, D96. Maps on the live map superseded by D123: they
wait for the activity that needs them. Extended by D127: sub-areas suggested by resources, percentages, and a preview.*

D93 left an activity's settings to 5b and 5c, and 5b did not write them.

- **What a module declares**: a class of the settings, with a constructor of no parameter, whose initial values are the
  defaults, and each of its properties it makes a setting, in code, as it registers the activity:
  `services.AddActivity<TActivity, TSettings>(id, name, settings => ...)` (`ActivitySettingsBuilder<TSettings>`, in
  `D3B.Bot.Settings`). A setting is named by an expression, `s => s.Walks`, whose type says what it holds; its name and
  help are `LocalizedText`s (D96).
- **The kinds** (`SettingKind`), D3B's, each with its editor in the app: `Flag`, a `bool`; `Number`, an `int` between
  bounds; `Text`, a `string` of bounded length; `Choice`, an enumeration's value, or a list of them (`Choices`), each
  named by the module; and the game's values by their ids, `Job`, `Area`, `SubArea` and `Resource`, an `int?` for one
  and a list of `int`s for several (`Jobs`, `Areas`, `SubAreas`, `Resources`). A list holds each value once; `Ordered`
  says its order matters, such as a priority. `Required` asks for a value. A sub-area is sought `Within` an area, a
  resource within a profession: the app offers those within the parent's value once it has one. `VisibleWhen` shows a
  setting when a condition of the settings holds, hidden it is not checked. `Section` groups the next settings under a
  title. `Rule` declares what the settings hold together, such as a minimum below a maximum, and what to say when they do
  not.
- **The game's values** (`GameReferences`, registered by the bots module): professions but the base, the atlas's areas
  and sub-areas, each sub-area's level, and the items each profession's skills gather, at the lowest level a skill
  gathers each. Read from the installed build's pack the first time something asks: a host that starts nothing never
  reads them. The app names them from the pack's texts in its language, and searches them whatever the case and accents,
  "frene" finding Frêne.
- **Checked in `D3B.Bot`** (`ActivitySettings.Check`), so a host without the app checks too: a problem per setting at
  most, of a kind the app says in its own language (`SettingProblemKind`: required, out of range, too long, unknown,
  repeated, not within), then each rule that does not hold, with the module's text. Without the pack, the game's ids are
  not checked. A condition or rule of a plugin's that throws shows the setting, or does not hold: the app runs it on its
  UI's thread, in the process that holds every client's connection (D91).
- **Registration checks the defaults** for what the module wrote wrong, a number out of its bounds, a text too long, a
  value repeated: the host does not start (D21). A value the user must give, such as a profession, has no default, and a
  rule may wait for it.
- **Stored as JSON**, a value per setting's key, its property's name; a choice by its value's name, so that an
  enumeration may be reordered. Reading starts from the defaults and takes each value that still reads: a key no longer
  declared is dropped, a value that no longer reads keeps its default and is reported (`SettingsReading.Reset`); of a
  list, what still reads is kept.
- **The activity receives them** through its constructor, an instance of the class, beside the services it asks for
  (`ActivityDefinition.Create(services, settings)`); started without a profile, with the defaults. `AddActivity<T>`
  without settings stays as it was (`ActivitySettings.None`).
- **The sample**: the stroll takes its number of walks and its reach as settings. **The template**: its activity takes a
  setting.
- **Later**: maps, picked on the live map (5d); monsters, once step 9 puts them in the pack. A plugin's own editor, for a
  setting no kind holds such as a path drawn on the map, stays possible beside the kinds.
- **Rejected.**
  - Attributes on the class, as DataAnnotations: least code, but an attribute holds no `LocalizedText`, the texts become
    keys resolved apart, and a wrong kind shows only to reflection.
  - Typed keys and a bag of values: no reflection, but the activity reads `settings.Get(Walks)` rather than an object.
  - An editor a plugin writes, with JSON of its own: every plugin writes its forms again, nothing is checked without the
    app, and the design splits.
  - The kinds alone, the game's values left to step 6: the mechanisms, lists, order, dependencies, conditions, are what
    is dear to add once activities rely on the settings.

## D99: Accounts seen, reusable profiles, a character's own

*2026-10-29, step 5, session 5c; the profile chosen by the owner. Builds D23, D65, D87.*

- **An account is never added by hand**: D3B sees no password (D23). It comes the first time its client lists its
  characters through D3B: the identification gives its id, nickname, tag and subscription (D72), the server's list each
  character's id, name and level (`KnownAccount`, `KnownCharacter`, in `D3B.Storage.Accounts`). The character the client
  chooses is dated as played. It may be forgotten, its characters with it, until its client lists them again.
- **A profile** (`Profile`, in `D3B.Storage.Profiles`) is a name, unique whatever its case, an activity by its id, and
  its settings as JSON (D98), shared by characters: changing it once changes it for all.
- **A character plays a profile, or none**, preselected on its bot's card; and may ask for it to start on connection,
  off by default: once its client first connects while the host runs, when its bot is made, the profile starts
  (`StartOnConnection`). Once per run: a new connection of the client carries the activity on (D89), and an activity
  the owner stopped stays stopped. A character with no profile never starts.
- **Recorded as the relay sees it** (`AccountRecorder`, an observer of the game connections, D62): dated as seen, queued
  off the relay's read loop, written one at a time, a write that fails logged and the relay going on.
- **A profile starts** (`ProfileStarter`) once its activity is registered, every stored value reads, and the settings
  hold in the pack; else it says why, and the bot is left alone: a setting reset is looked at in the app before the bot
  runs on defaults it was not given.
- **Groups**, a master and its followers, come at step 10, over this model.
- **Not measured**: recording and starting on connection on a client through the app; the tests play them through game
  connections built as the relay shows them.
- **Rejected.**
  - A profile chosen on the card at each start, kept by no character: nothing remembered, nothing starts by itself.
  - A profile of a character's own: the same settings copied for every character that does the same.
  - A profile per account: a bot is a character (D65), and an account's characters rarely do the same.

## D100: D3B's database: SQLite through EF Core, in a project of its own, no secret

*2026-10-29, step 5, session 5c. Builds D15, D91, D95.*

- **`D3B.Storage`**, not packed: EF Core 10.0.14 on SQLite, its context (`StorageContext`), its migrations, and its
  module (`StorageModule`). Not in `D3B.Bot`: the package plugins compile against would bring EF Core, and SQLite's
  native libraries into every plugin's folder, which D95 checks. Plugins do not see it: profiles are D3B's, and only
  name the activities plugins register.
- **Shared by the app and `D3B.Host`**: both add the module to the host's composition (`BotHost`), so both read and write
  one file, `%LOCALAPPDATA%\D3B\d3b.db`, beside the packs, captures and the client's configuration, or `Storage:Path`.
  Connections are not pooled, so the file is not held while D3B is idle, and foreign keys are on: forgetting an account
  forgets its characters, deleting a profile leaves its characters without one.
- **The migrations apply before the host starts**, through a step any module may add (`IHostPreparation`, in
  `D3B.Bot.Hosting`), run once the game data and the protocol are ready: a database that does not open stops the host,
  which says why and how to name another file, as for a missing pack (D31). `dotnet tool run dotnet-ef migrations add
  <Name> --project src/D3B.Storage --startup-project src/D3B.Storage --output-dir Migrations` writes the next, through a
  design-time context that needs no host; `dotnet-ef` 10.0.14 is a local tool (`dotnet-tools.json`). Migrations are
  generated code, which the analyzers and the formatter leave alone.
- **In memory too**: the accounts and the profiles are read once the database opens, then again after each write, a few
  rows; the app reads them each refresh without the database, and a version number tells it they changed
  (`AccountBook`, `ProfileBook`). Writes go one at a time per book.
- **No Data Protection**: nothing kept is a secret. Ids, nicknames, character names and settings are personal data, not
  secrets; the ticket is never kept (D65); the file lives in the user's own profile. D15's "Secrets" row waits for the
  first secret D3B keeps, such as a Discord webhook at its step.
- **Rejected.**
  - The storage in `D3B.Bot`: see above.
  - Migrations applied by a hosted service as the host starts: a failure would escape the host's start rather than say
    why it did not start.
  - Data Protection now: it would protect nothing.

## D101: The app's pages: a rail always, accounts by account, profiles in a column

*2026-10-29, step 5, session 5c; both layouts chosen by the owner among mockups. Supersedes D96 in part: the rail
shows always. Builds D94, D96, D99. Its editor grown by D127: tiles, a zone's mini-map, figures, bars and segments.*

- **The rail** shows always: the bots, the accounts, the profiles, a line once a module registered a page, then the
  modules' pages (D96). Its icons are the app's own line drawings.
- **The accounts page**: a card per account, the one seen last first: nickname and tag, its subscription's end or that it
  has none, when D3B last saw it or that a bot of its plays now, and a button that forgets it, which asks first. Its
  characters are rows: initials with a dot of their bot's connection, name, level and when they last played, the profile
  they play, chosen from a list, and the switch that starts it on connection, enabled once they have one. An account
  explains where it comes from: its client going through D3B, which never sees its password.
- **The profiles page**: a column of profiles, by name, a new one first. Folded, a profile says its name, its activity,
  three of its settings at a glance, and the characters that play it; to look at again when its activity is gone or a
  stored value no longer reads. "Edit" unfolds it in place, one at a time: its name, its activity, then each section of
  its settings in two columns as the width holds, each setting by its kind, its bounds, help and problem, the rules that
  do not hold below, and save, enabled once all hold, cancel, and delete, which asks first. Changing the activity starts
  its settings again from their defaults. While a profile is edited, what the relay records does not disturb its editor.
- **A bot's card** offers the profiles first, by name, with their activity beside, then the activities with their
  defaults; the profile its character plays is chosen. An activity whose settings need a value asks for a profile; a
  profile that does not start says why.
- **The language and the theme are remembered** in a file of the app's own, `%LOCALAPPDATA%\D3B\app.json`
  (`AppPreferences`), read before the window shows, while the host and its database still get ready: the window opens as
  it was left. A file that does not read leaves the defaults. This is not the database: the app reads it before the host
  runs.
- **Drawn without a screen** in both languages and both themes, and looked at; the window's tests read both pages' texts in
  French and English.
- **Rejected.**
  - For profiles, cards with an editing panel on the right: the form, which the game's values will lengthen, squeezed into
    a narrow panel. A list beside a permanent editor: the three panes the owner turned down in 5a.
  - For accounts, a table of characters grouped by account.
  - The rail shown only beside a module's page: the app has three pages of its own.

## D102: Lua scripts replace the external .NET plugins

*2026-10-29, step 5, session 5c, chosen by the owner. Supersedes D90, D95, D97, and D6, D21, D22 and D96 for external
plugins; brings Q1 and step 7 back. Changes the brief. Superseded by D116: Lua scripts go too.*

The owner chose to drop the external .NET plugins for a Lua scripting system, once 5c showed what remained before the
plugins' packages could be published: trimming the six packages' public API, about 180 types without the protocol's
generated classes, to what a plugin needs, which the app, `D3B.Host`, the storage and the file-based tools all made
hard. The lead recommended finishing 5c, then deciding after a prototype of Lua, which Q1 never got; the owner decided
now.

- **What a user extends the bot with** is Lua scripts: routes and routines, as the bots sold and the owner's profession
  scripts in `vendor/metiers` are written. The brief's "External .NET plugins add modules" becomes: users extend the bot
  with Lua scripts. D3B's own features stay modules (D5, D20), in the repository, which may disable and replace them.
- **Why**: a small API made for scripts, in place of every public type of six packages kept stable; the language the
  ecosystem uses, so that the owner's scripts are adapted rather than written again in C#; no compiling, no packages,
  a file reloaded.
- **What stays**: modules, activities (D87), whose script will be one kind of `BotActivity`; settings (D98), whose kinds,
  checks, editor and profiles serve scripts too, a script declaring its settings in a table rather than a class;
  profiles (D99), naming a script as they name an activity; the pages of D3B's own modules (D96), registered as now.
- **What goes**, in a session of its own: the plugin loader and its attribute, the `plugins` folder, packing `D3B.Bot`
  and the five it names, CI's template check, the `dotnet new d3b-plugin` template, and the sample plugin, its stroll
  written again as a script. Q11's question, what a plugin sees, has no object.
- **Step 7, Lua scripting, comes back**, its number kept. Its first task settles Q1: LuaCSharp behind a `BotActivity`, a
  step calling the bot's behaviors and awaiting them, cancelled by an interruption and resumed, a script's settings,
  errors told in the app, no access to files or processes; a piece of one of the owner's scripts ported. The API's
  namespaces start from D12's (`global`, `map`, `inventory`, `npc`, `job`, `exchange`, `character`) and the owner's
  scripts, a script's `move()` and `bank()` as their steps (D87). LuaCSharp's version is the latest stable then (D15).
- **Risks.** Q1 is unproven: LuaCSharp may not hold up, and the decision would be looked at again. A script brings no
  page of its own.
- **Rejected.**
  - Deciding after a prototype, the lead's recommendation.
  - Lua beside the plugins, as D12 had it: two APIs to keep.
  - Keeping the plugins (D90), their API trimmed and frozen before the first milestone.

## D103: LuaCSharp holds, Q1 settled

*2026-10-29, step 7. Settles Q1. Confirms D15's "Scripts" row: LuaCSharp 0.5.8, of 2026-10-25, is still the latest on
NuGet, every version so far a 0.x.*

A prototype ran Lua scripts behind a `BotActivity` (`ScriptActivity`, in `src/D3B.Scripting`, which D90 had removed
empty), on the bots the tests play (D67), then the API of D105 over it. Measured:

- **A call that waits for the bot.** A C# function of the API is a `LuaFunction` that awaits a behavior, such as
  `bot:walk`, with the step's token; the script resumes once it is over. A Lua function kept in a table is called from
  C# after an `await`.
- **A step cancelled, then resumed.** An interruption cancels the call the script awaits: it comes out as
  `LuaCanceledException`, an `OperationCanceledException`, which the run turns into the interruption (D87). `pcall` does
  not catch it, nor any `OperationCanceledException` a C# function throws, so a script cannot swallow an interruption; it
  does catch every other exception, as a Lua error. The script's globals and the locals its functions hold stay as they
  were: the next step starts from the state with them.
- **A script that never waits.** LuaCSharp checks the token as the script loops, about every 100 ms measured, so a
  `while true do end` stops with its activity; an endless recursion is a stack overflow, a Lua error.
- **No files, no processes** (`ScriptSandbox`): the base library without `dofile`, `loadfile`, `load`, which also loads
  bytecode, and `collectgarbage`, which drives the process's collector; `string` without `dump`; `table`, `math`,
  `bit32`; of `os` its clock and dates alone. No `io`, `package`, `require`, `debug` or `coroutine`. The platform under it
  refuses files, the environment and an exit, should a library come to ask. `print` writes to the script's console.
- **Errors told in the app.** A script's error fails the activity with a `ScriptException` reading as Lua's,
  `mineur.lua:3: attempt to index a nil value (local 'mine')`; the run keeps it (`ActivityRun.Failure`), the bot's card
  shows it in the danger tone, and the script's console (`ScriptConsole`, its last 200 lines) ends with it. Drawn without
  a screen.
- **The owner's scripts**: their 58 files and 17 settings files compile as they are.
- **Limits of LuaCSharp 0.5.8**: writing the stack of a cancellation inside a function throws, which `ScriptException`
  works around; "attempt to call a nil value" does not name the field, where C Lua does; an error raised by a function
  called in a tail call has no line, as in C Lua; strings are UTF-16, `#"é"` being 1; nothing bounds a script's memory,
  `string.rep("x", 1e8)` allocating what it asks.
- **Rejected.**
  - A hook counting instructions to stop a loop: LuaCSharp's own check of the token does it.

## D104: MoonBot among the references, and what the owner's scripts call

*2026-10-29, step 7, MoonBot added by the owner. Adds to D3.*

`vendor/moonbot/lua-api.md`, the documentation of MoonBot's Lua API, a bot sold for Dofus 3, joins D3's references, for
ideas only, as Snowbot's family: D3B copies none of its text and none of what D2 excludes, which it documents too
(reconnections and their staggering, subscription purchases, raw packets).

Measured on the latest version of each of the owner's five profession scripts (`vendor/metiers`), what they ask of a bot:

- **37 functions** of 9 namespaces, `global:printMessage` 113 times, then `global:delay`, `global:printError`,
  `developer:createMessage` and `sendMessage` 35 times each, `map:currentMapId`, `job:level`; 30 of the 37 exist under the
  same name in MoonBot's, so Snowbot's and MoonBot's names are one family. MoonBot lacks `printColor`, `getTeamMembersIds`,
  `Get/AddInGlobalMemory`, `getLastChallengeProposalMessage` and `character:id`.
- **Routes**: `move()` and `bank()` return a table of steps by map, run on the map the character stands on: 2,461 `path`,
  2,091 `gather`, 70 `door`, 43 `custom`. Paths are sides (2,224), `havenbag` (108), `zaap(id)` (80), cells, sides to
  choose from at random (`top|right`) and sides from a cell (`left(392)`). Globals the bot reads: `GATHER`,
  `PODS_THRESHOLD`, `OPEN_BAGS`, `MIN_MONSTERS`, `MAX_MONSTERS`.
- **Settings**: a `settingsX.lua` beside the script, loaded with `pcall(dofile, ...)`.
- **The protocol**: the fight's challenges, by name (`ChallengeSelectionRequest`), and one request by an obfuscated name.
- A dump of the readable names of the game's messages and fields that a third party published, given by the owner, fits
  3.7.12.12: 1,344 of its 1,345 obfuscated types are in the build's schema (D39), and where D3B's mapping pairs a message
  the dump names the same type, under the same name 43 times and another 16, where D3B chose its own words (D42). It is
  data derived from the client, of no license: never committed, read at most for ideas when the readable protocol grows
  (D3, D11).

## D105: The Lua API is D3B's own

*2026-10-29, step 7, chosen by the owner, against the lead's recommendation of the family's names. Builds D87, D102. Superseded by D116.*

The lead recommended the names of Snowbot's and MoonBot's family (D104), so that the owner's scripts run nearly as they
are; the owner chose an API of D3B's own. The form is the lead's, local and reversible:

- **Hooks**: a script defines the hooks of `BotActivity` (D87), each given the `bot`: `step(bot)`, required, a step chosen
  from the state, `false` ending the activity; and optionally `isFull(bot)`, which reads and never acts, `unload(bot)`,
  `onInterrupted(reason)`, answering `"stop"` to end the activity, and `recover(bot, reason)`, run after the default
  recovery (D89). Reasons are `player`, `disconnected`, `fight` and `stopped`.
- **The text runs first**, once per start, to define the hooks and declare the settings (D107). It cannot act: the `bot`
  is no global. Once it ran, its global `settings` holds the values. A first step cut short before it ran whole runs it
  again in a new state; a character full once it ran is unloaded before the first step.
- **The bot** (`BotObject`): fields read the state live, `bot.character`, `bot.map` (id, coordinates, world map,
  sub-area), `bot.cell`, `bot.inventory` (weight, load, kamas, items), `bot.jobs`, `bot.isBusy` and the like; methods act
  through the hook's behavior and wait: `walk`, `changeMap` (a side or a map action's cell), `travel`, `use`, `bank`,
  `leaveHavenBag`, `wait`, `waitIdle`, and `count`, `jobLevel`, which read. An action answers as Lua's own functions,
  `true`, or `false` and why, the behavior's outcome in lower camel case: `local ok, why = bot:walk(301)`.
- **Globals**: `log.info`, `log.success`, `log.error` and `print`, to the script's console and the bot's log; `game`,
  the pack's data: `mapsAt`, `position`, `distance`, `cell`, `mapActions`.
- **Names** in lower camel case; methods with `:`, functions of a namespace with `.`; a `:` call to a namespace's function
  reads as a `.` one.
- **Not yet**: harvesting, fights and the rest of the milestones, which the API gains as D3B's behaviors do; routes as the
  owner's tables, which step 13 may bring as a helper of D3B's own.
- **Rejected.**
  - The family's names, `global:`, `map:`, `move()` returning a route by map, the lead's recommendation: the owner's
    scripts adapted rather than written again, but a dialect of others' with its quirks, `podsP` and two calling forms.
  - Both, D3B's API and the family's over it in Lua: two surfaces to document, test and keep in step.

## D106: Scripts reach the protocol through D3B's readable requests

*2026-10-29, step 7, the lead's recommendation, which the owner left to the lead. Frames the family's
`developer:createMessage` and `sendMessage`. Superseded by D116.*

- `bot:send(name, fields)` sends the game server a request of D3B's readable protocol, named as its `.proto` files name
  it, whole (D55), its fields by their JSON names, in lower camel case as the rest of the API; every field is checked
  against the message's descriptor, an unknown field or a value that does not fit failing the call (`ProtocolTables`).
  It goes through the hook's behavior, between the client's frames (D61), and the client is not told.
- `bot:waitFor(name, ms)` waits for the next event or response of that name, from now on, and gives the fields the
  readable protocol reads, an enumeration's value by name; `nil, "timeout"` when it does not come. `bot:request(name,
  fields, answer, ms)` expects the answer before it sends, as every behavior does (D67).
- Never raw bytes, never a message to the client, no obfuscated name.
- **Risk.** A request the client does not follow may leave it out of step with the server (D60): the script's author
  answers for it, and the definitions say so.
- **Rejected.**
  - No protocol at all: the client always consistent, but what has no behavior yet, such as the owner's challenges, waits.
  - Ankama's original names beside D3B's, from a table per build shipped as the mappings are: closer to the family, but a
    table per build to make, of names D3B does not choose.

## D107: A script's settings, a table onto D98's kinds

*2026-10-29, step 7, the form chosen by the owner. Builds D98, D99. Superseded by D116.*

- **Declared as data**, an entry per setting in the order the app shows them, `{ key, kind, name, help, default, min,
  max, maxLength, options, required, ordered, within }`, or `{ section = ... }`; kinds `flag`, `number`, `text`,
  `choice` and `choices` among the entry's options, and the game's `job`, `area`, `subArea`, `resource`, with an `s` for
  several. A text is a string, or a table by language, `{ fr = "...", en = "..." }` (`LocalizedText.ByLanguage`). Read
  without the bot (`ScriptSettings`), a declaration that does not read says what and where.
- **Into D98's settings**, without a class: `SettingValues`, a value per key, and `SettingValuesBuilder`, whose settings
  are `SettingDefinition`s like a class's, a choice's values being the declaration's own strings rather than an
  enumeration's. The app edits them, a profile stores them as JSON by key, checks and resets apply as for a module's; a
  profile names a script as it names an activity (D99).
- **Not yet**: conditions and rules, which are code in D98 and would run a script's function on the app's thread.
- **Rejected.**
  - Builder functions, `setting.number(...)`: the same data, with functions to document beside it.

## D108: Scripts live in a watched folder, with the API's definitions

*2026-10-29, step 7, the folder chosen by the owner; the definitions asked for by the owner. Superseded by D116.*

- **Where**: `%LOCALAPPDATA%\D3B\scripts`, or `Scripting:Directory`, beside the database: every `.lua` file of it and of
  its folders is a script, but the definitions below. The scripting module (`ScriptingModule`), which the app and
  `D3B.Host` add as they add the storage's, registers the `ScriptLibrary`.
- **Listed as activities**: `script:` and the file's path, such as `script:metiers/paysan.lua`, shown as its path without
  the extension. The library is a source of the `ActivityCatalog` (`IActivitySource`), which lists the modules'
  activities then the sources', and changes `Version` with them; the bots' cards and the profiles page list them again
  when it changes.
- **Read without the bot** (`ScriptReader`): the text runs in its sandbox, 2 s at most, its hooks found and its
  settings read. A script that does not read is listed still, as an activity whose start fails with its error, which the
  card shows; the log says why.
- **Reloaded**: the folder is watched, and once quiet for 300 ms every script is read again, the list replaced whole. A
  start takes the text as last saved; an activity running keeps the text it started with.
- **Documentation and completion**: D3B writes into the folder `d3b.lua`, the API as LuaCATS annotations, and
  `.luarc.json`, Lua 5.2 and the libraries a script lacks, as the host starts, when they differ: opened in VS Code with
  its Lua extension, the folder has the API's documentation and completion. A test checks that `d3b.lua` documents every
  hook, field, method and function of the API, and nothing else.
- **The sample**: the former sample plugin's stroll, `samples/scripts/promenade.lua`, played by the tests.
- **Rejected.**
  - Scripts in the database, edited in the app: renaming safe, but an editor to write, and VS Code lost.
  - A running script reloaded when its file changes: its state lost mid-activity, and a file half saved breaking a bot
    that runs.

## D109: The external plugins removed

*2026-10-29, step 7. Carries out D102. Supersedes in part D19, whose `samples/` now holds example scripts; `samples/`
goes with them, by D119.*

- **Gone**: the plugin loader and its attribute, its load context and `PluginInfo` (`D3B.Core.Plugins`), the `plugins`
  folder and `Plugins:Directory`, packing `D3B.Core`, `D3B.Bot` and the four libraries it names, CI's check of the
  template, the `dotnet new d3b-plugin` template, and the sample plugin, its stroll written again as a script (D108).
- **Stays**: modules (D5, D20), D3B's own, in the repository; activities (D87) and their settings (D98); the pages of
  D3B's modules (D96), in the app's rail, a page's view model now `ModulePageViewModel`; a page that throws still shows
  its error, tested with a page of the tests' own.
- **Why** (D102): no package's public API to keep stable; users extend the bot with scripts, which bring no page.

## D110: Lua scripts, measured through the app

*2026-10-29, step 5, session 5e. Completes D103 and D108, which only the tests had measured.*

Measured on a client of 3.7.12.12 through the app, in two runs, the scripts' folder holding the sample stroll,
`promenade.lua`, and a script failing on purpose at its second step, never committed
([client.md](client.md#the-game-from-the-app)):

- **Listed**: the host read both, neither failing, and the bot's card offered them among its activities.
- **Started from the card**, with its defaults, 10 walks: stopped from the card during its 9th walk, it ended at once,
  `Stopped` after 8 steps; run whole, its 10 walks arrived and it ended `Done`.
- **Interrupted by the player, then resumed**: the player's click during its second walk paused the bot, the activity
  answering that it resumes; resumed from the card 8 s later, it went on from the cell the player had led the character
  to, the script's counter where it was, and ended `Done`.
- **A failing script** failed at its second step, `Failed` after 1, its error with its line, the exception whole in the
  log.
- **Closing the app** gave the client its configuration back and closed the game connection.
- **Found**: the error names the script without its extension, `erreur:10:`, where D103 has `mineur.lua:3:`: LuaCSharp
  is given the name the app shows as the chunk's. Not fixed, Lua scripts going (D116).
- **Not measured on the client**: a script started from a profile, none being saved; what the card showed of the failure,
  which the owner did not report, drawn without a screen in D103.

## D111: The logs and the protocol inspector, two pages of the rail

*2026-10-29, step 5, session 5e; the layout chosen by the owner among three mockups, the filters as the lead proposed
them, chosen by the owner. Builds D92, D101.*

- **Two pages** in the rail, after the profiles: the logs, and the protocol inspector. They look at one bot
  (`BotChoice`): chosen on either, it is the other's. A bot's card has a journal button, beside its connection, which
  opens the logs page on its bot.
- **The logs page**: its sources as chips, every source by time, the host, then each bot with a dot of its status; the
  lines' tone as a segment, every line, warnings and errors, or errors; a search through what the lines say, whatever its
  case; "Follow", which scrolls to each new line; "Copy", the lines shown as text. A line says its time, a mark of what it
  is in its tone, its source when every source shows, what happened, then its outcome and duration (D112); a problem of
  the host has its whole message and exception as a tooltip (D114).
- **The protocol inspector**: a bot's frames live, chosen among chips, or a capture of the game connection, chosen in
  "Replay a capture…", the last written first, its folder read again as the list opens. Filters: the client's, the
  server's and D3B's frames, a toggle each; a search through the messages' names, readable or the build's; "Hide unread",
  off at first; "Follow"; "Freeze", live only, the frames waiting until it thaws; and clearing the list. A row says its
  time, its sender, its message, its size, and how far the readable protocol read it: understood whole, read, or not
  read. A frame unfolds in place, which stops following, so that it stays in view (D115).
- **Drawn without a screen** in both languages and both themes, and looked at; the window's test reads both pages in
  French and English and opens a bot's journal from its card.
- **Used on a client** of 3.7.12.12 through the app by the owner: the journal opened from the card, a stroll paused by
  the player and resumed, the filters, the inspector live and a capture replayed, no problem reported
  ([client.md](client.md#the-game-from-the-app)).
- **Rejected.**
  - One page of tabs, journal, script console and frames, the bot chosen at its top: one tab at a time, when the journal
    and the frames are read together.
  - A drawer on the right of the bots page, opened from a card: lines and frames squeezed into a panel, the cards beside
    it.
  - Unread frames hidden at first: three frames of four (D58), but what is hidden is what the one mapping a build looks
    for.
  - Richer filters, by category of the host's log and by time.

## D112: A bot's journal says its actions, not the protocol

*2026-10-29, step 5, session 5e, asked for by the owner: each action, using an element, changing maps, never the
protocol's messages.*

- **Written by the behaviors** (`BotActor.Journal`, entries in `D3B.Bot.Journal`): an action once over, with its outcome
  and how long it took by the bot's clock: a walk to a cell (`WalkEntry`), a map change, by a side, a map action, a door,
  a zaap or leaving the haven bag (`MapChangeEntry`), a use of an element with a skill (`UseEntry`), a travel and its
  number of changes (`TravelEntry`), banking and the stacks and kamas it put in (`BankEntry`). Then what becomes of the
  bot, connected, disconnected, paused by the player, resumed, a fight beginning (`BotEventEntry`), and of its activity,
  started, interrupted and what it answered, unloading, over and how, its error included (`Activity*Entry`).
- **Actions within actions** (`BotContext.BeginAction`): an action is written once over, as deep as the actions it was
  begun within. A travel's map changes come before it, one deeper, and the app sets them in; a walk made for another
  action, to a map's exit or an element, has no entry. An action an interruption cuts has none either: the interruption's
  entry says it.
- **Ids, never words**: an entry names maps, cells, elements and skills by the game's ids; the app names them from the
  pack in its language (`JournalLines`), a map by its coordinates, the sub-area beside them where the place is the point,
  such as a travel's end, an element by what it is and the skill used, and by its id what the pack does not name.
- **Rejected.**
  - The host's log, filtered by bot: the protocol's messages and the host's English, the lines the owner turned down.
  - Every walk, those made for another action included, set in under it: twice the lines for what the action says.
  - No walk at all: an activity's own walks would go unseen.

## D113: The logs and the frames kept in memory, always

*2026-10-29, step 5, session 5e, chosen by the owner.*

- **Kept in the host**, in memory, from its start, the app open on any page or none: each bot's last 2,000 journal
  entries (`BotActor.JournalCapacity`), the host's last 2,000 problems (`HostProblems.Capacity`), and each bot's last
  3,000 game frames, both sides and D3B's, across its connections (`FrameRecorder.Capacity`). Older ones are dropped
  (`RecentItems<T>`), and the pages let go of what the host no longer keeps.
- **Read as they come**: a page reads only what came since its last refresh, every 250 ms, and only while it shows.
- **A frame costs a reference**: the game session decodes it once (D62), and the recorder keeps it.
- **Nothing written to disk**: captures stay what records a connection (D37), and a host that stops forgets the rest.
- **Rejected.**
  - The inspector recording only while a button is on: what came before it was opened, such as the ticket's request and
    the character's entering, is what one looks for.
  - 10,000 lines and frames per bot: about 15 MB per bot, for what a capture keeps better.

## D114: The host's problems on the logs page

*2026-10-29, step 5, session 5e, chosen by the owner.*

- **A source of its own**, "Host": what the host's log writes at the warning level or above, unless it names a bot by
  its character, whose journal says it already (D112). `HostProblems`, an `ILoggerProvider` the bots module registers,
  keeps them as the host writes them, in English, with the exception if any: a script that does not read, a connection
  whose account is unknown, a relay's failure.
- **Rejected.**
  - No such source, the host's problems left to `D3B.Host`'s console, which the app has none of.
  - The host's whole log, information included: the relay's every connection and the database's commands, in English.

## D115: The protocol inspector

*2026-10-29, step 5, session 5e. Carries out D92's 5e, with D63's replays.*

- **Recorded by the host** (`FrameRecorder`, in `D3B.Bot.Inspection`), an observer of the game (D62) the bots module
  registers: a game connection's frames are its character's once the server confirms the character the client chose, as
  bots are tied to theirs (D65), those that came before joining them then; a connection whose account D3B does not know
  is never a bot's, and its frames go with it.
- **D3B's own requests** reach observers apart (`IGameObserver.OnSent`, which does nothing by default), decoded as the
  server reads them, once sent: `OnFrame` still never shows them (D63), the bots learning what they did from the server.
- **A frame unfolded**: a readable message's fields as protobuf's JSON names them, those it holds, at every depth, an
  enumeration's value by name, bytes by their length, a list of scalars past 32 values counted (`FrameFields`). A message
  the readable protocol lacks, or that does not read, shows its shape alone, its fields' numbers, wire types and lengths,
  as `scripts/capture.cs --frames` does (D37); `WireReading` and `WireField` are public for it.
- **The ticket is never shown**: the game connection's first request, `AuthenticationRequest`, shows it is there,
  hidden, live as in a replay, as no log names it (D65).
- **Replays** (`CaptureFrames`): a capture of the game connection is read whole, each side's reads framed again as the
  relay would, D3B's frames being records of their own (D61), through the installed build's translation. A capture of
  another build is refused, saying which, since its frames name messages only its own mapping reads; a capture cut short
  says so.
- **A frame's length**: `DecodedGameFrame.Length`, without its varint, which a row shows.
- **Measured** on a client of 3.7.12.12 through the app ([client.md](client.md#the-game-from-the-app)): its capture of
  298 frames replayed whole, the client's, the server's and D3B's 21; each frame not read showed its shape; the
  `AuthenticationRequest` came first, its ticket hidden; the character chosen, which ties the frames to their bot.

## D116: Lua scripts go, in a session of their own

*2026-10-29, step 5, session 5e, chosen by the owner, against the lead's recommendation. Supersedes D102 and D105 to
D108; D103 stays as what was measured. Changes the brief. Carried out by D119; nothing outside the repository superseded
by D117, mods coming as blueprints.*

Once the scripts were measured through the app (D110), the owner chose to do without them. The lead recommended
freezing them as they are, no more work on their API, and deciding again after step 6.

- **What a user extends the bot with**: nothing outside the repository. Every feature is a module of D3B's own (D5,
  D20); activities (D87), their settings (D98), profiles (D99) and the modules' pages (D96) stay.
- **What goes**, in a session of its own: `D3B.Scripting` and its tests, the scripting module, the scripts' folder and
  `Scripting:Directory`, `d3b.lua` and `.luarc.json`, `samples/scripts/` and its stroll, and LuaCSharp. Profiles naming
  a script then name an activity that no longer exists, which the profiles page shows (D101).
- **In 5e**: no script console tab on the logs page; the chunk's name D110 found is not fixed.
- **The order**: the removal next, then 5d; a client update before a session still gets Q10's first (D54).
- **Rejected.**
  - Freezing them, the lead's recommendation: an API and its definitions kept, unused.
  - Removing them within 5e: one large task per session (D17).

## D117: Mods in blueprint style, after step 6

*2026-10-29, the session removing the Lua scripts, chosen by the owner, the step's place as the lead recommended it.
Supersedes D116's "nothing outside the repository" for mods. Changes the brief. Opens Q13.*

Asked which activity D3B keeps without Lua (D118), the owner answered that mods will be made in blueprint style: graphs
of nodes, edited on a canvas in the app. The references the owner gave, read for their look and ergonomics only (D3),
all React where the app is Avalonia (D14):

- shadcn.io's Canvas, a node editor for agents' workflows: nodes as cards with a header, content and footer and their
  input and output handles, curved edges, animated or dashed, panning, zooming, fitting, a toolbar on the node chosen,
  keyboard control; written without a flow library.
- React Flow's Workflow Editor template: nodes dragged from a sidebar, laid out automatically (ELKjs), and a runner that
  executes the nodes one after another and shows where it is.
- ReUI's flow blocks, on React Flow: nodes as cards with their status, duration and owner, labelled edges such as yes
  and no, an inspector beside the canvas that reads and edits the node chosen, snapping to a grid, a context menu.

- **When**: after step 6, as step 7, whose number Lua held. A node wraps a behavior of D3B's, and harvesting's, which
  step 6 writes, do not exist yet; step 6's harvesting is a module's activity (D87), the first milestone.
- **What is open** (Q13): who writes mods, the owner or users too; where they live; what a node is over D3B's behaviors
  and state; how a graph runs as an activity, interrupted and resumed from the state (D87); its settings (D98); and which
  canvas in Avalonia, a library or the app's own.
- **Risk**, the lead's: a graph that runs is a language to keep, what Lua cost (D116); its contract is to stay small.
- **Rejected.**
  - The blueprints right after 5d, before harvesting: a runner and nodes over behaviors not written yet.
  - The blueprints before 5d.

## D118: No activity of D3B's own until step 6

*2026-10-29, the session removing the Lua scripts, chosen by the owner, against the lead's recommendation. Builds D93.
Ended by D127: the harvesting module registers D3B's first activity.*

Without Lua, no module in `src/` registers an activity: the scripts were the only ones, the tests' own aside.

- **The catalogue is empty** until step 6 writes harvesting (D93): a bot's card says there is no activity to start yet,
  and that D3B's modules will add them; the profiles page has no activity to give a new profile.
- **What waits**: starting, stopping, pausing and resuming an activity from the app on a client, and profiles, their
  settings and starting on connection, which D99 and D110 did not measure on a client, until step 6. 5d measures the
  live map with what the player does.
- **Rejected.**
  - The stroll written again in C#, a module's activity with its two settings, the lead's recommendation: 5d would see
    the bot walk on its own, and profiles would be measured on a client, for about a hundred lines to keep or drop at
    step 6.

## D119: The Lua scripts removed

*2026-10-29, the session removing the Lua scripts. Carries out D116. Supersedes D107's settings without a class and
D108's source of activities, the owner's choice against the lead's recommendation, and D109's `samples/`.*

- **Gone**: `D3B.Scripting` and its tests, the scripting module the app and `D3B.Host` added, the scripts' folder and
  `Scripting:Directory`, `d3b.lua` and `.luarc.json`, `samples/scripts/` with its stroll, and `samples/` with it, empty,
  and LuaCSharp from the central packages. CI named none of them.
- **D107's pieces go too**, the owner's choice: the lead recommended keeping them for the blueprints (D117), whose
  mods' settings are data and whose activities come and go. Gone: `IActivitySource`, the catalogue's `Version` and the
  app's lists read again when it changed; `SettingValuesBuilder`, `SettingValueBuilder` and `SettingValues`; choices of
  strings; `LocalizedText.ByLanguage`. `SettingDefinition` is a property of the settings' class again, as before step 7;
  the catalogue holds the modules' activities alone, fixed once the host is built (D93).
- **Kept**, serving any activity: `ActivityRun.Failure` and the error on the bot's card (D103); an activity that is
  `IDisposable` disposed once over, its run being the only one to hold it (D93); `WorldPathfinder.Atlas`, which the card
  places the character with.
- **A profile naming a script** names an activity gone, and nothing maps it to another: the profiles page says
  "Activity gone" with its id, `script:promenade.lua`, and edits it only once given another activity; deleting it works.
  A bot's card offers it, the gone activity beside it, and starting it says the activity is gone.
- **On connection**, a profile that does not start says why in the bot's journal (`ProfileNotStartedEntry`, in
  `D3B.Storage.Profiles`), its activity gone, its settings to review or not holding: the host's log names the bot, which
  the host's problems leave to the journal (D114), and the journal said nothing of it, the bot doing nothing in the app
  with no reason shown.
- **The user's folder** `%LOCALAPPDATA%\D3B\scripts` stays as it is: D3B neither reads nor writes it any more, and the
  owner deletes it.
- **Measured** on a client of 3.7.12.12 through the app, on a copy of the database holding a profile that names
  `script:promenade.lua`, its character's own, started on connection ([client.md](client.md#the-game-from-the-app)): the
  host built its five modules, no scripting among them; the profile did not start on connection, which the bot's journal
  said; the card and the profiles page showed its activity gone; the inspector live and a capture replayed; closing the
  app gave the client its configuration back. Not measured on the client: deleting the profile, which the tests do; the
  player's pause, which needs a behavior running, and so an activity (D118).

## D120: The live map, a page of the rail

*2026-10-29, step 5, session 5d, chosen by the owner among three mockups, as the lead recommended it. Carries out D92's
5d.*

- **A page** in the rail, after the profiles, before the logs: "Map". It looks at one bot, chosen among chips as on the
  logs page and the inspector, and shares their choice (`BotChoice`, D111): a bot chosen on one is the others'. A bot's
  card has a map button beside its journal button, which opens the page on its bot.
- **Its header**: the map's coordinates large, then where the character is, the sub-area and the cell, in a fight, in a
  haven bag or off the atlas, then what it is in the middle of, as its card says it (D94), and the map's id; the layers'
  toggles beside it, the legend under the map.
- **Read every 250 ms** while it shows, as the other pages (D94): a new scene only when the bot's snapshot, the other
  bots on its map, its connection, its pause or the app's language changed.
- **Rejected.**
  - The map on the bot's card alone: each card some 200 px taller, against D94's whole cards, its cells some 10 px high,
    too small for the elements and to point at.
  - A thumbnail on the card that opens the page: it repeats what the card says large, drawn again for every bot. Still
    possible later, the control being the page's.

## D121: The live map shows what the pack and the state hold, in layers

*2026-10-29, step 5, session 5d, chosen by the owner, as the lead recommended it.*

- **Seven layers**, each toggled: the cells, walkable, obstacles that block the sight and voids that let it through, and
  the grid; the character, its path as the server showed it and its end; the interactive elements, placed from the pack
  and marked by what the server says of them, a resource to harvest, harvested, another element, a door, or not
  described, grey; the NPCs, a banker named from the pack, any other by its id; the exits, each side's cells with the
  neighbour's coordinates, the map actions and the doors with the maps they lead to, from the world graph, criteria not
  answered; the other bots of D3B on the map; and the zones and stairs, hidden at first, tinted where a map has more than
  one zone, or both move zones (D86).
- **A cell under the pointer** is described in a card beside it, in the app's language: its number and coordinates,
  walkable with its floor and zone, a stair, an obstacle or a void, then what stands on it: the character, the path's
  start or end, another bot, the element with the skills it offers or that it is harvested, the NPC, and where an exit
  leads.
- **The exits** (`MapExits`): a scroll goes with every walkable cell of its edge that leaves the map that way
  (`MapGeometry.LeavesTowards`, moved from `WorldPathfinder` for it), each cell with the nearest scroll of its zone and
  direction (D71); a map action by its cell; a door by its element, on the element's own cell.
- **In a haven bag** the bag's map is drawn as any other, "Haven bag" in place of the place. **In a fight** the fight's
  own map is drawn, its coordinates left out (D88); the character shows once the server shows it moving, its placement
  not being in the readable protocol (D125).
- **Out of 5d**: other players and monsters, which the readable protocol does not read among the map's actors (D55),
  steps 9 and 10; walking to a cell clicked, a command of its own; the world map.
- **Rejected.**
  - The cells, the character and its path alone: the rest is in the state and the pack already, and costs nearly as
    much later.
  - Other players and monsters too: the protocol, the state and the mapping to extend and measure, steps 9 and 10.

## D122: The live map, drawn flat from the pack

*2026-10-29, step 5, session 5d, chosen by the owner, as the lead recommended it.*

- **A control of the app's** (`MapCanvas`, in `D3B.App.Controls`) draws the cells read from the pack, as diamonds of 14
  columns by 40 staggered rows, fitted to its room with margins for the exits' labels (`MapLayout`). What a map holds,
  its walkable cells, obstacles, stairs, zones and exits, is built into geometries once per map; what moves is drawn
  again each refresh.
- **Flat**: floors drawn in height would overlap cells on a map with stairs; the stairs are tinted in the zones' layer,
  and a cell's floor is given under the pointer.
- **The pointer** finds its cell from the cells' geometry (`MapLayout.CellAt`), never by testing the shapes drawn.
- **Colours** from the theme: a palette per theme (`MapPalette`, in `App.axaml`'s theme dictionaries), the dark's and
  the light's; the legend draws each mark with the map's own drawing (`MapMarkIcon`).
- **Nothing to extract** or publish: the pack of 3.7.12.12 holds it all.
- **Rejected.**
  - The client's textures: 63,454 graphics in three scales, the smallest 1.3 GiB on disk and 6.3 GiB decompressed,
    assembled per map in three layers with effects and shaders; an extractor, a pack of textures per build released
    (D24) and a renderer, more than the rest of step 5, for a map prettier and harder to read.

## D123: Maps for settings wait for the activity that needs them

*2026-10-29, step 5, session 5d, chosen by the owner, as the lead recommended it. Supersedes D98's "maps, picked on the
live map (5d)".*

- **Not in 5d**: a setting of maps, its kind `Map` or `Maps`, comes with the first activity that needs it, at step 6 or
  later. What it holds, one map or several, ordered as a route or a set, within a sub-area, is that activity's to say.
- **The control is reusable**: `MapCanvas` draws any scene, a map and what stands on it, and says the cell under the
  pointer, so that a setting's editor may show a map.
- **Rejected.**
  - The kinds `Map` and `Maps` in 5d, a field of the profiles page and "Add to a profile…" on the map page: a third of a
    session measured only by the tests, no activity existing before step 6 (D118), its shape guessed.
  - The same with a world map to pick from: a page of its own.

## D124: The live map, measured through the app

*2026-10-29, step 5, session 5d.*

Measured on a client of 3.7.12.12 through the app, a game connection of 2 min 49 s, the player moving the character by
hand and no activity started (D118), captures on ([client.md](client.md#the-game-from-the-app)):

- **Followed**, the owner reported, in every case: walks on the map of a zaap, the haven bag entered and left there, a
  scroll to a neighbour, the haven bag again from it, then a fight, and the map after it.
- **The maps**, five, all in the pack: two of the world graph, the haven bag's, off the graph at 0,0 on no world map, the
  fight's own, off the graph at 0,0, and a zaap's, where the fight left the character, neither the map it began on nor a
  neighbour of it. The page drew each.
- **The player's walks**, 13, each confirmed; the server showed 13 more of other actors.
- **No frame from D3B**, nothing in the host's log above information.
- **Found**: neither the fight's map nor the map it began on holds a cell marked `red` or `blue` in the pack (D125).

## D125: A fight's placement cells, from the server at step 9

*2026-10-29, step 5, session 5d, asked for by the owner, chosen as the lead recommended it.*

The owner asked for the map to mark the red and blue cells in a fight, where each team is placed.

- **At step 9**: the cells the server gives when a fight begins, its message mapped and measured, then marked on the
  map. In 5d, nothing.
- **Why not the pack's**: the cells marked `red` and `blue`, whose meaning is Dofus 2's, not measured (client.md), are on
  neither the fight's map measured nor the map it began on; of the 2,877 maps at 0,0, off the world graph and on a world
  map, as the fight's is, 1,158 hold some. In the same millisecond as the fight's context, the server sent 16 frames the readable
  protocol lacks, the placement likely among them.
- **Rejected.**
  - The pack's flags tinted on a fight's map from 5d: little code, but nothing for the fight measured, and a meaning not
    measured.
  - The server's message mapped in 5d, from this session's capture, and a fight measured again: protocol work the size
    of a session, a mapping format 7, outside step 5.

## D126: Step 6 in three sessions, the icons in their own

*2026-10-29, step 6, session 6a; both chosen by the owner, as the lead recommended them. Superseded in part by D136: the
icons are stored as raw pixels, not PNG.*

- **6a**: the first milestone itself: the harvesting module and its activity, its settings and their editor, the next
  resource and the next map, banking full and the way back, the activity resumed after a fight, a new connection or the
  player's pause, and what the bot's card and the map page show of it; measured on a client through the app.
- **6b**: icons, in the pack, the owner asking for them beside resources and items: the 254 items the sub-areas list as
  harvestable and the 23 professions, decoded from the client's BC7 textures and stored as PNG in a pack of format 4, about
  1 to 2 MB, released as every pack is (D10, D24); the elements' graphics with them, which would let a tour know a zone
  before it goes there (D128). Measured: `Picto/Items` holds 12,279 item icons, 64 px at 1x and 128 at 2x, one sprite
  each named by the item's `iconId`, in BC7; the pack keeps no icon and no `iconId`.
- **6c**: what 6a's measures leave: fights while harvesting, the resources' growing back, harvest statistics, the
  guild's chest the owner noted, and options the owner listed for later, such as opening resource bags and healing
  under a share of life, each a request and an event to map; then the owner's call on making the repository public (D4).
- 6a's views keep the icons' place, a neutral square, which 6b fills.
- **Rejected.**
  - 6a cut into the activity, then the app: the owner wanted to verify in the app from 6a on.
  - Every icon, 12,279, in a pack of their own: 50 to 60 MB to download, for an app showing fewer than 300.
  - The icons read from the installed client as they show: nothing distributed, but the Unity readers and a BC7 decoder
    in `src/`, with their tests, reading bundles in the process that holds every connection (D91).
  - The icons in 6a: an extractor, a decoder and a release more in a session the milestone filled.

## D127: The harvesting module, its settings, and the profile editor

*2026-10-29, step 6, session 6a; the module and settings as the lead recommended them, the owner asking for more: resources
of any profession, sub-areas suggested by them, the zone's figures, the levels missing, and an editor more beautiful,
chosen among two mockups. Builds D87, D93, D98, D101. Maps as settings still wait (D123).*

- **The module** (`HarvestingModule`, in `D3B.Bot.Harvesting`), in `DefaultModules` after the bots module, registers the
  activity `harvest` (`HarvestActivity`), the host's sightings (D128) and the previews below; its texts are a `.resx` of
  its own in English and French (D96), `D3B.Host` running it without the app. `Register` adds it to a host that builds its
  services apart, as a test's.
- **The settings** (`HarvestSettings`), in three sections: what it harvests, resources of any profession in their order,
  the first harvested first, or everything each character can harvest at its level; where, an area and sub-areas, one or
  the other at least, an area alone standing for its sub-areas that list one of the resources, sub-areas chosen of any
  area; when it banks, pods full at a share from 50 to 100 %, 90 by default, and what goes to the bank, what harvesting
  brings or everything but what is worn, no kamas, which pay the zaaps and the bank's opening (D80).
- **D98 gains three mechanisms**, for any activity: a sub-area setting suggested by a resource setting (`SuggestedBy`), the
  sub-areas where they grow offered first, the most of them first, then the lowest (`GameReferences.SubAreasHolding`); a
  number that is a percentage (`Percent`), shown as a bar; and a preview the activity reckons from its settings and the
  pack (`Preview`, `SettingsPreview`), figures and the maps of its zone. `GameReferences` takes the world graph, so that a
  sub-area counts the maps a character may travel to, and says who gathers a resource (`TryGetSource`).
- **The harvest's preview** (`HarvestPreviews`): the maps of the zone, the resources asked for its sub-areas list, and the
  changes from the nearest bank's front to the zone, a door more, for a character of whom nothing is known. Measured on the
  pack of 3.7.12.12: 34 sub-areas hold ash or chestnut, suggested in 14 ms; the preview of a zone of 128 maps, its path to
  the bank included, in 50 ms.
- **The editor**, the generic one of D101 grown: resources as tiles in their order, each with its profession and level, the
  icon's place, and a warning when a character playing the profile lacks the level, as its bot last saw it; the zone's
  sub-areas beside its mini-map, each map a square at its coordinates on the world map from the atlas, the zone's parts
  tinted, the maps around dim and the nearest bank marked (`ZoneMapCanvas`), the sub-areas suggested as rows a click chooses,
  and the preview's figures; the share of pods as a bar, and a choice of three values at most as segments. A field may take
  a whole row of its section (`CardGrid.IsWide`). Drawn without a screen in both languages and both themes, and looked at.
- **Rejected.**
  - A project `D3B.Harvesting`: a project more for a module, `DefaultModules` living in `D3B.Bot` with the behaviors it
    calls.
  - The area required: sub-areas of two areas could not mix.
  - An editor of the harvest's own: the mechanisms serve every activity, the blueprints of step 7 among them.
  - Maps as settings in 6a, a route or maps to avoid (D123): the sub-areas carry the milestone.

## D128: The next resource and the next map: tours through the zone, the nearest first

*2026-10-29, step 6, session 6a, chosen by the owner, as the lead recommended it; the insides last after a measure.*

The references, read for ideas (D3): BillBot alone chooses by itself, the nearest resource on a map, and between maps a
search scoring each by the time since its visit, capped at an hour, times its worth and a random factor, the maps never
seen first; it learns, while playing, which graphic is which resource, and keeps it on disk. The owner's profession scripts
follow fixed routes by level. None measures how long a resource takes to grow back.

- **On a map**: of the elements the server offers a wanted resource's skill on, at the character's level, a profession
  not described yet counting as the first, the resource asked for first, then the nearest by its walk; one taken by
  another, refused or out of reach is left for the visit.
- **A tour** (`HarvestTour`): a map with nothing left is gone through, and the step travels to the nearest map of the
  zone still to go through, stopping on the first of the way; once every map is, the next tour begins. A tour that
  harvested nothing waits up to a minute on its map for a resource to grow back before the next sweeps the zone again.
- **What the host's bots saw** (`HarvestSightings`): each map's resources, harvested ones included, as the server
  described them; a tour leaves out the maps seen holding none of its own. In memory, shared by the host's bots, nothing on
  disk, as the banks' insides (D79).
- **The insides last.** Measured on the client: a first tour went through a forest's houses for minutes, a journey of 12
  changes through a zaap among them, before its first harvest. Measured on the pack: 114 of the 186 sub-areas listing
  resources hold insides too, 1,493 maps against 6,569, and the mines are insides of such sub-areas, 36 of them with a
  miner's resource. So a tour goes through the maps of the world map first, then its insides (`HarvestZone.Insides`).
- **Failures**: a map the way to failed leaves the tour; three failed journeys in a row end the activity, which says why
  (D131); a zone with no map, or none holding a resource, ends it too.
- **Rejected.**
  - BillBot's scoring: chance and weights nothing measured sets, for what the tours give, the maps gone through longest
    ago first.
  - A route the user draws: maps as settings, later (D123).
  - The nearest alone on a map: the owner asked for a priority.
  - The insides left out: the mines are insides.

## D129: Banking full, and the way back

*2026-10-29, step 6, session 6a, chosen by the owner, as the lead recommended it; the reason said after a measure.*

- **Full** at the settings' share of pods, before each step (D87). **Unloaded** at the nearest bank (D79, D80): what the
  settings say goes, in one request, no kamas; two more tries after a failure that may pass, no answer or a journey
  missed. **The way back**: the next step starts from the state, the character off the zone, and travels to the nearest
  map of the tour, zaaps included for a subscribed account (D83); the map it left stays in the tour while it held
  resources.
- **Still full once banked.** Measured on the client: the bank took the harvest, 3 stacks, and the 4,958 pods left of
  7,590 were still above the share, the character carrying other things; the activity failed with nothing said on the
  card. It now ends there with its reason in the app's language, the weight left and what to change, to bank everything
  but what is worn or raise the share; any activity left full says so (`ActivityRun.IsStillFull`).
- **Risk**: the bank's opening costs a kama per stack the storage holds (D80); a character without them is not measured.
- **Rejected.**
  - A bank chosen in the profile: the nearest carries the milestone.
  - The guild's chest: 6c, its messages to map.
  - Back to the exact map: no position kept (D87); the tour brings it back when it is worth it.
  - Going back to the bank at every step while still full: the reason said instead.

## D130: A fight, a new connection, the player's pause: the activity resumes from the state

*2026-10-29, step 6, session 6a, chosen by the owner, as the lead recommended it. Builds D87 to D89.*

- A fight, a protector's or an aggression or the player's own, interrupts the step; the activity waits for its end,
  the bot not paused (D88), recovers, and the next step finds where the character is: in the zone it goes on, elsewhere it
  travels back, full it banks first. Until step 9 the player fights.
- A new connection takes the bot back (D65, D89); the tour and the sightings stay, the bot outliving its connections.
- The player's pause stays D66's, resumed from the card.
- The activity says what it waits for (D131).
- **Rejected.**
  - Stopping after a fight: the character left where it fought. An option for it may come in 6c, with the fights while
    harvesting.

## D131: An activity says what it does: its report, on the bot's card and the map page

*2026-10-29, step 6, session 6a, chosen by the owner among mockups, as the lead recommended it.*

- **The report** (`BotActivity.Report`, `ActivityReport`, in `D3B.Bot.Activities`), any activity's, replaced whole by the
  activity and read by the app every 250 ms (D94): what it does, how far it is (`ActivityProgress`), what it counts
  (`ActivityCount`), its warnings, and what it looks at on the map (`ActivityFocus`): the element it goes for, those it
  wants, the next map, and the maps of its zone with where it stands in each (`ZoneMap`). An activity that cannot go on
  says why (`ActivityException`).
- **Its texts name the game's values by their ids** (`GameText`, `GameTextValue`, in `D3B.Bot.Localization`), as the
  journal does (D112), the app naming items, professions, sub-areas and maps in its language (`GameTextFormatter`); a text
  that counts gives its plural, which the app chooses as its language has it.
- **The bot's card**: what the activity does, its tour as a bar with its map and the time it ran, what it harvested of
  each resource with the icon's place, the banks, and the levels missing; how it ended, its reason included. A run
  remembers the name it was started under (`ActivityRun.Title`): a profile started on connection shows by its name.
- **The map page**: a line of what the activity does and how far; a layer, Activity, ringing the elements it wants, the
  one it goes for brighter, and the exit it heads to; and beside the map, the tour's mini-map, the maps gone through, to go
  through and seen empty, with the character.
- Drawn without a screen in both languages and both themes, and looked at.
- **Rejected.**
  - A harvest's own display: the report serves every activity, the blueprints' among them.

## D132: Banking lets the client show its windows

*2026-10-29, step 6, session 6a. Builds D77, D80.*

- **Measured** on a capture through the app: D3B replied 1 ms after the banker's question and left the bank 39 ms after it
  opened, the whole bank in 130 ms; the server closed the dialog and the bank, and the client asked to leave each, but both
  windows stayed on its screen, the owner reported. The player, in the same capture, replied 771 ms after the question.
- **So** banking waits half a second, D77's, after the question, after the bank opens, and before leaving a dialog it could
  not take to the bank (`Banking.ShowDelay`). Measured again: the reply 547 ms after the question, the deposit 1,085 ms after
  the action, the bank left at 1,118 ms, the client's two requests to leave at 1,152 and 1,179 ms; the windows closed.
- This is no timing shaped to look human (D2): the client must have shown a window to close it, as it must have drawn a
  map to follow a walk.

## D133: Harvesting, measured through the app

*2026-10-29, step 6, session 6a.*

Measured on a client of 3.7.12.12 through the app, captures on, four runs of the app, the owner's character harvesting
nettles, its profile "Ortie" ([client.md](client.md#the-game-harvested)):

- **From map to map**: harvests on one map after another, the tour going on to the nearest map still to go through.
- **A fight** interrupted the activity, which went on once it was over, with no command.
- **The player's pause**: the player's click during a journey paused the bot; resumed from the card, it went on.
- **A new connection**: the client closed during the activity; the new connection took the bot back, and it went on.
- **Stopped** from the card, it ended at once.
- **A profile started on connection**, never measured before (D99, D118): three times, the profile started once the
  client chose the character.
- **Full**: to the bank of Bonta through a zaap and its door, the banker 6394, the stacks put in, the bank left; still full
  once, the reason said (D129); the windows left open, then closed (D132); then back to the zone through a zaap.
- **Found**: the houses of the zone first (D128); a journey of 12 changes to maps of the zone reachable only far round; walks
  the client did not confirm (D85) on a map of the zone, the journey going another way.

## D134: No human timings

*2026-10-29, step 6, session 6a. The owner asked for human timings, as BillBot's; they stay out (D2).*

- D2 keeps out of D3B every timing shaped to avoid Ankama's detection, BillBot's `human_timings` named among them, and
  `CLAUDE.md` says it again. D3B's delays are those the client needs to follow what the server shows it, each measured:
  D77's for a map, D132's for a window.
- Should the owner want to change D2, it is a change of the brief, decided apart; this session does not add them.

## D135: The icons of 6b: the items a harvest names, and the professions

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it. Builds D126.*

- **Measured** on client 3.7.12.12 (client.md): the 254 items the sub-areas list as harvestable, and the 33 a skill of
  `Base` gathers, such as drinking water from a well, potatoes or shells, which no sub-area lists: 287 items, drawn by
  only 121 icons, loot boxes sharing theirs. 22 of the 23 professions have an icon, 21 in all, two sharing one; `Base` has
  none. `Picto/Items` holds 12,279 icons.
- **In the pack**: those 121 icons and the professions' 21, by their identifier; items and jobs keep theirs (D139). An
  item without an icon in the pack keeps the neutral square.
- The owner noted, choosing, that a bot harvests more than professions' resources, wells among them: their icons are in,
  and harvesting them waits for 6c (D140).
- **Rejected.**
  - The 254 items alone: the wells and the rest of `Base`'s would keep the neutral square.
  - Every icon, D126's: 50 to 60 MB.
  - Characters' classes, which the bot does not know yet, and monsters', at step 9.

## D136: Icons 64 pixels square, in raw RGBA

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it. Supersedes in part D126: no PNG.*

- **Size**: items' icons at 1x, 64 pixels, professions' at 2x, 64 pixels too. The app draws them from 16 to 34 pixels; the
  owner's main screen is 2560x1440 at 100 %, and 64 pixels stay sharp up to 188 %.
- **Format**: 8 bits a channel, red, green, blue, alpha, the alpha straight, rows from the top, one array per kind of icon
  (`IconTable`), which the pack's zstd 19 compresses. Measured: items' icons at 1x take 474 KB so, 500 to 620 KB as PNG.
- **Why not PNG**: larger, an encoder to write, and bytes depending on the zlib of .NET's runtime, where the pack must
  stay deterministic (D25); the app makes a bitmap from the pixels as they are.
- **Rejected.**
  - Every icon at 2x: 1.4 MB, the same at 100 %.
  - PNG, D126's.
  - The client's BC7 blocks as they are, decoded by the app: a decoder in `src/`, which D126 rejected.

## D137: BC7 decoded by AssetRipper.TextureDecoder, in the extractor alone

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it.*

- `scripts/extract.cs` references AssetRipper.TextureDecoder 2.6.3: MIT, managed, for .NET 10, without dependency, the
  decoder AssetsTools.NET.Texture uses. Nothing in `src/` decodes: the pack holds pixels (D136). Added to D15.
- `IconsExtractor` reads each icon's texture and sprite by the typed readers, and fails on a texture that is not BC7,
  64 pixels square, one level, its texels in the bundle's `.resS`, or whose sprite's rectangle does not cover it whole; the
  rows are turned top first, Unity storing the bottom first.
- **Measured**: the 142 icons decoded right, looked at; the bundle of 12,279 item textures read in 1.4 s.
- **Rejected.**
  - A decoder of our own: about 400 lines of BC7's partition tables, without tests in `scripts/` (D10).
  - BCnEncoder.Net: an encoder first, with two dependencies.
  - Kyaru.Texture2DDecoder, AssetStudio's, which Dofus Remastered uses: native binaries per system.

## D138: The maps' graphics, learned by the host, foretell a tour

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it; what is learned changed after a measure.
Builds D128.*

- **Measured** (client.md): the client's data never says which interactive an element is, its dictionary of element
  definitions included; the server says it of each element of the map a character enters. On 31 captures of 6a, the
  elements one graphic draws are mostly one resource: nettle's elements share one graphic, on 775 maps of the world. Fishing
  spots share one between seven fish. Learned on all captures but one, the graphics foretold the last right 102 times of
  103. The 53 sub-areas listing nettle hold 3,133 maps, 750 of them drawing its graphic.
- **In the pack**: each element's graphic, `MapInteractive.GfxId` (D139).
- **What the host learns** (`GraphicSkills`, `GraphicLearner`): for each graphic, every skill the server says its elements
  offer, enabled or not, none for an element that offers none; from every map's information any bot receives, whatever
  its activity. Only the elements the server lists teach: measured, it leaves out a third of the pack's elements of the
  maps it describes, some drawn with a resource's graphic elsewhere.
- **Where it is kept**: D3B's database, a table `Graphics`, its skills as JSON (`GraphicRecorder`), read before the host
  starts, written as the bots learn; from one run to the next, and from one client build to the next, a graphic being an
  asset of the client; a contradiction only adds a skill. Whether a client update renumbers graphics is Q14's.
- **What a tour does with it** (`HarvestZone.Foresee`, `HarvestTour`): a map never seen is probable when one of its
  graphics offers a skill the zone uses, without a resource when every one is known and none does, a map without elements
  included, and unknown otherwise; what a bot saw of a map wins. A tour goes first to the probable maps, the nearest
  first, then to the others of the world map, then to the insides, and leaves out those without a resource.
- **In the app**: the profile's preview counts the probable maps and marks them on the zone's mini-map, the maps left out
  dim; the tour's mini-map shows them apart, with a legend (D141).
- **Rejected.**
  - Kept in memory, as the maps seen (D128): every run of the app would learn again, and the preview could not foretell.
  - Every element of a described map teaching, the unlisted ones as offering nothing: some draw a resource's graphic.
  - A table of graphics learned from the maintainer's captures, in the pack: the server's word in a pack of the client's,
    and only the maps the maintainer went through.
  - Nothing in 6b, the tours staying 6a's.

## D139: Pack format 4, and its release

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it. Builds D24, D25, D33.*

- `GamePack.FormatVersion` is 4: `Item` and `Job` gain their `IconId`, `MapInteractive` its `GfxId`, and one entry,
  `icons/catalog` (`IconCatalog`), holds the items' and the professions' icons apart, the two numbering theirs apart; read
  once by `GameTables`. The fingerprint covers the icons' two bundles. `PackFormatTests` records format 4's layout.
- **Measured** on 3.7.12.12: 5,382,920 bytes against 4,670,325, extracted in 18.4 s, two extractions giving the same
  SHA-256; the icons 0.58 MB compressed, 2.33 MB read in 15 ms; the graphics 0.10 MB more on the maps, the icons' ids
  0.03 MB on the items.
- **Release**: `data-4.pack` and its SHA-256 go to the release `client-3.7.12.12` beside `data-3.pack`, which older hosts
  keep, through `scripts/release.cs --publish`, on the owner's go.
- **Rejected.**
  - A pack of icons of its own: a second source and a second download for 0.6 MB.
  - An entry per icon: 142 entries for a catalog read whole in milliseconds.

## D140: Base's resources harvested in 6c

*2026-10-29, step 6, session 6b, chosen by the owner, as the lead recommended it.*

- The harvesting activity of 6a harvests professions' resources only. Wells, potatoes and the rest that skills of `Base`
  gather wait for 6c: no sub-area lists them, so only the graphics learned (D138) say where they are, and their settings
  have no profession's level. Their icons are in the pack already (D135).
- **Rejected.**
  - In 6b, whose icons, extractor, release and graphics filled the session.

## D141: The icons in the app

*2026-10-29, step 6, session 6b; the lists, the profession's icon and the map asked for by the owner while verifying.*

- **Where**: the resource tiles, their icon on its plate, their profession's beside the level; the searches adding a
  resource, the resource's icon and the profession's beside its detail; the bot's card, beside what it counts; the live
  map, a resource drawn by its icon over its cell, faded once harvested. The pack's icons become bitmaps once per pack
  (`GameIcons`); a pack without icons, or an item without one, keeps the neutral square.
- **The owner's asks**: a search's detail under its name, so that its list keeps its width as it scrolls, in the three
  searches of the profile editor; the profession's icon at 20 pixels, at 14 too small to read; the resources' icons on the
  map.
- Drawn without a screen in both languages and both themes with icons of the tests' own, discs of a colour, and looked at.

## D142: 6b measured through the app

*2026-10-29, step 6, session 6b.*

Measured on a client of 3.7.12.12 through the app, captures on, the owner's profile "Ortie", nettle and sage in Amakna
([client.md](client.md#the-game-foretold)):

- **The icons**: the owner saw them on the resource tiles and in the search adding one.
- **Learning**: a first run, nothing learned before, entered 43 maps in 3 minutes and learned 35 graphics, which the database
  kept for the next run.
- **Foretelling**, from the knowledge each map was entered with: 18 maps foretold probable, 13 of which held nettle or sage,
  11 harvested, the 5 others drawing a resource's graphic only on elements the server did not list; 5 foretold without a
  resource, none holding one; 20 unknown, 1 holding one.

## D143: A pass on the app's UI, UX and options, in a session of its own

*2026-10-29, step 6, session 6b, chosen by the owner.*

- Verifying 6b, the owner noted many small things to improve in the app's interface, its ease of use and its options:
  margins, paddings and sizes inconsistent across the app, and many of ShadUI's components left unused. A pass over every
  page, in a session of its own after 6c: 6d in the roadmap. 6b fixed only what it drew (D141).
- **Rejected.**
  - The pass in 6b: every page of the app, past what this session drew.

## D144: A fight while harvesting: resumed, paused or stopped, as the profile says

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D88, D130.*

- **Measured** on the 34 captures before 6c (client.md): 4 fights. 3 the player's, the client sending its walk's
  confirmation and a request of 49 bytes the readable protocol lacks, the attack, in one read; 1 a resource's protector
  during 6a's activity, beginning 2 ms after the end of the character's own harvest, with no request from the client or
  D3B, over 64.5 s later. No aggression captured; how a fight ended is not decoded, and no message read gives a fight's
  kind: an event of 38 bytes, one `int64`, comes before each, but often elsewhere too.
- **The kind, told by its timing**: a protector's when the fight begins right after the end of the character's own
  harvest; an aggression's when the bot was acting and nobody attacked; the player's when the bot was paused, the player's
  walk having paused it (D66). The journal says it, and the bot's card counts fights.
- **A setting**, "After a fight", in three segments: resume, the default and D130's; pause, the bot paused as by the
  player's hand, resumed from its card, its tour and counts kept; stop. Until step 9 the player fights.
- **To measure**: an aggression, and a fight lost, where the character comes back and with how much life.
- **Rejected.**
  - Stopping on every fight: a protector would cost the tour.
  - Avoiding protectors: they come at random.
  - Giving the fight up: a fight's action, step 9's.

## D145: The resources' growing back, learned by the host

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D128, D138.*

- **Measured** on the captures before 6c: no growing back seen whole, the character never staying on a map long enough.
  Bounds alone: nettle seen grown back at most 445 s after its harvest; trout not yet 297 s after, grown back at most
  564 s after. A harvest lasts 3 s, drinking water from a well 1.5 s. The server never says when an element will grow
  back: it shows it once it has, and on entering a map. A bound seen on a later visit holds only from above, another
  player having maybe harvested the element meanwhile.
- **What the host learns**, for each resource: how long it takes, exactly when a bot stayed on the map until its element
  grew back, else the smallest upper bound a later visit gave; kept in D3B's database, as the graphics are (D138).
- **What a tour does with it**: a map whose wanted elements all still grow back waits until they should have; once every
  map of the tour does, the bot waits on its map until the first should have, rather than D128's minute. A resource
  whose growing back is not known yet keeps D128's behavior.
- **In the app**: the tour's mini-map shows the maps growing back; the bot's card says when the next one should.
- **To measure** through the app: exact times, and whether one resource always takes the same; if not, the longest seen.
- **Rejected.**
  - A table of times written by hand: 85 resources to measure, and the next build may change them.
  - Measured alone, in client.md: a small zone would still be gone through for nothing.
  - Learned in memory alone: learned again at every run of the app.

## D146: Harvest statistics, in D3B's database and a page of their own

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it.*

- **Each run kept**: the character, the profile, when it started and ended and how, the items it brought by item, each
  profession's experience gained, the banks and the fights by kind; written at each banking and at its end, so that a
  crash loses little. Structured, not the harvest's own: any activity may give its figures.
- **A page, Statistics**, in the rail: by character and period (today, 7 days, everything), four figures (resources,
  time, rate an hour, levels gained), each resource with its icon and rate, and the runs.
- **On the bot's card**, live: each resource's rate an hour, and the experience gained.
- **Rejected.**
  - Live alone, nothing kept: what the owner asked to see over time would go with the app.
  - In the profile editor: figures belong to characters as much as to profiles, and the editor is long already.
  - Their worth in kamas: the marketplace's prices, step 11's.

## D147: The guild's chest, measured first

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D80, D129.*

- **Known**: no capture opens it. JondoEmu, read for ideas (D3) and never tested against Ankama's server, has it an
  element in banks, graphic 70671, with tabs whose rights go by rank and a new tab at guild levels 3, 7 and 11, opened as
  a storage exchange like the bank's. The pack of 3.7.12.12 draws that graphic in 21 insides, all in the world graph.
- **Two settings** in the banking section: where it deposits, its own chest in the bank or the guild's, and the tab.
  The guild's chest of the nearest bank holding one takes what the bank would, no kamas; refused, for want of a guild,
  rights or room, the character deposits in its own chest and the card says so.
- **Measured first**, in a capture the owner makes by hand: opening the guild's chest, changing tabs, putting a resource
  in and taking it back, and a deposit refused if one can be: the requests and events to map.
- **Rejected.**
  - The guild's chest chosen whenever the guild has one: the owner's harvest in the guild's pool without asking.
  - Stopping on a refusal: the deposit has a place to go.

## D148: Upkeep: resource bags opened, the character healed under a share of its life

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it.*

- **Known**: item type 100, "Sac de ressources", holds 73 items of the pack, such as "Sac de blé"; the pack keeps each
  item's type. Nothing needed is mapped: using an object, which both opening a bag and eating are, the character's life
  and its most, and sitting.
- **A section, Upkeep**, in the profile: a flag opening each resource bag as soon as a harvest gives one, between steps, so
  that what it holds is banked with the rest; a share of life, off by default, under which the bot heals before its next
  step, eating the items the profile lists in their order, else sitting until its life is whole. The card shows the life
  above the pods.
- **Measured** in the owner's capture of D147: opening a bag by hand, sitting, eating, the life written down before and
  after, compared on the machine and never written in the repository.
- **Rejected.**
  - Healing by waiting alone: slow without food.
  - Healing at step 9, with fights: the protectors raise it now (D144).

## D149: A zone's isolated maps, left out unless the profile takes them

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Builds D128, D133.*

- **Measured** on the pack of 3.7.12.12: 67 of the 186 sub-areas listing resources fall apart through their own maps'
  transitions, and 588 of their 8,062 maps lie off their sub-area's main part, the part holding the most maps. From it, 54
  are 2 to 4 changes away, 128 are 5 to 35, as 6a's journey of 12, and 416 are reached only by a teleport or a door's
  criterion. The worst, sub-area 601: 222 maps, 141 in its main part, in 25 parts.
- **Isolated**: a map of a sub-area off its main part, more than 4 changes from it through the world graph, teleports
  aside (`HarvestZone.Isolated`, `MostDetour`), reckoned with the zone.
- **Left out by default**; a flag, "Go through the isolated maps too", takes them, last in a tour, a part at a time. The
  profile editor's mini-map shows them, dashed, and its preview counts them.
- **Rejected.**
  - Kept always, last: the long journey would come back each tour.
  - Left out always: some isolated parts may be rich.

## D150: Base's resources harvested as the others

*2026-10-29, step 6, session 6c, chosen by the owner among mockups, as the lead recommended it. Settles D140.*

- **Known**: 34 skills of `Base` gather 33 items, drinking water from a well, potatoes, shells and events' items among
  them, all at level 1; no sub-area lists them; their icons are in the pack (D135). The owner's database knew 46 graphics,
  none offering a skill of `Base`. An index of every map's graphics, 10,981 maps and 3,345 graphics, takes 564 ms.
- **Offered** among the resources, without a profession or level (`GameReferences.IsBase`); a tile and a search say on how
  many maps the learned graphics place one, or that it was never seen and will be learned on the way
  (`GameReferences.PlacesOf`, `GraphicPlaces`).
- **Placed** by the learned graphics: the sub-areas suggested for them, and those an area alone stands for, are those
  holding a map that draws a graphic known to offer their skill.
- **Harvested** as the others: the probable maps first, the unknown learned on the way, no level asked; "what harvesting
  brings" banks them. "Everything each character knows" stays the professions': `Base`'s skills gather events' items too.
- **Rejected.**
  - The wells only.
  - A table of wells from the maintainer's captures, in the pack: D138 rejected it.
  - Learning at once from the owner's earlier captures: a tool more for what the first visit teaches.

## D151: The client's display time, and every delay of D3B in a settings page

*2026-10-29, step 6, session 6c; the owner asked for longer delays, the bots doing "anything" on small machines, then for
a page holding every delay. Builds D77, D132.*

- **Why**: D3B waits half a second after the server placed the character on a map before acting (D77), and half a second
  for a window to show before closing it (D132), both measured on the owner's machine. On a slower one the client has not
  drawn yet, and what D77 and D132 measured comes back: walks the client does not follow (D85), windows left open.
- **One setting for the machine**, not a profile's: the client's display time, 0.5 s by default, up to 3 s, wherever D3B
  waits for a map or a window. The other delays, how long D3B waits for the server's answers, a walk's arrival, a use's
  end, a recovery, and the relay's, also become settings, each with its measured value by default and a button to restore
  them.
- **A page, Settings**, at the foot of the rail: the client, the delays, the captures (from the next start), the language
  and theme (kept in the title bar too), and where the database and the pack are. Kept in `app.json`; `D3B.Host` reads the
  same values from its configuration. 6d and 6e add their own.
- **Rejected.**
  - A switch for slow machines, doubling the delays: no value to choose.
  - Measured by D3B from the client's pace: longer to make and to check.
  - Random delays between a least and a most: see D152.

## D152: The owner's harvesting settings in 6e; no human or random timings

*2026-10-29, step 6, session 6c.*

- **6e, chosen by the owner**: settings of harvesting and journeys, each submitted with mockups: no zaap when the
  destination is too near; an automatic mode choosing the zone by the character's level; the highest-level resources
  first, without leaving the others; lists of resources to take in or leave out; profiles given by default, such as the
  automatic harvest; and what the owner adds.
- **Asked and refused**: delays longer and "more human", then drawn at random between a least and a most. A random delay
  helps the client in nothing, which needs only enough time to draw; it serves only to look human, to escape Ankama's
  detection, which D2, D134 and `CLAUDE.md` keep out. The owner asked to remove the line from `CLAUDE.md`; the lead did
  not, and will not write such timings whatever the file says. D151's delays are the client's own, measured, raised by
  hand where a machine needs it.

## D153: Upkeep and the guild's chest, measured: mapping format 7

*2026-10-29, step 6, session 6c, in a session of its own. Amends D147 and D148 with what the owner's capture measured
([client.md](client.md#the-game-kept-up)).*

- **Named and pinned** for 3.7.12.12 (D54): `ObjectUseRequest`, one request to eat and to open a bag;
  `ObjectDeletedEvent`; the characteristics, partial (`CharacterStatsEvent`), holding the life; `LifeRegenEndEvent` and
  `LifeRegenStartEvent`, partial; `ExchangeStartedWithTabbedStorageEvent`, partial, of the exchange type 21, now
  `EXCHANGE_TYPE_GUILD_CHEST`; `ExchangeObjectMoveRequest`; `StorageObjectUpdateEvent`; `StorageObjectRemovedEvent`, partial.
- **D148, the life.** The most life is characteristic 0 plus 11, the life that plus 97, the life lacking; out of fights it
  regenerates a point every half-second, the interval the server gives, from the characteristics' life up to the most. The
  card's life and the share under which the bot heals read it so.
- **D148, healing.** Sitting changes nothing, the owner saw: the bot eats the profile's items one at a time, each answered
  in 28 ms with the life healed, and with none left waits for the regeneration, sitting never. A use at full life is
  refused with a text, nothing used. Using several at once is measured, the server using only what the life lacks, but not
  named: its request holds a map, which the tests' obfuscation does not make.
- **D148, bags.** A bag opens with the same request, its content joining the inventory, its stack lessened or deleted. A
  barrel of type 184, "Conteneur", opens the same way; D148 names type 100 only, and the lead decides whether to open other
  containers.
- **D147, the chest.** It is an element of the bank's inside, used with skill 184, with no NPC's dialog. Stacks go one at a
  time, whole or in part, by `ExchangeObjectMoveRequest`; the bank's list was not tried on it. An object bound to the
  account is refused with text 356 and stays in the inventory, to go to the character's own chest as D147 says; the
  inventory's objects carry effects 981 or 983 then, which the readable protocol does not read. It holds no kamas.
- **D147, the tab.** Not measured: the owner's guild has one tab. Until a guild with several is captured, the bot deposits
  in the tab the chest opens on, and the tab's setting waits.
- **Mapping.** The readable protocol holds 129 messages, 263 fields, 19 enums and 147 values. `MappingFile.FormatVersion` is
  7, `mapping-7.binpb`, 8,926 bytes, the same bytes on two runs, audited without failure; the 36 game connections captured
  decode through it, 25,258 frames, none misread. The release of 3.7.12.12 needs it, which the release tool publishes
  (D50) once the owner says so.

## Open questions

Settled questions leave this table, and the decision that settles one says so.

| Id | Question | Settled in |
| --- | --- | --- |
| Q5 | Which fight formulas of Bubble emu (Dofus 2) still hold in Dofus 3? | Step 9 |
| Q10 | Does every client build shuffle and renumber the game's fields, and how is a build's game mapped from the previous build's: messages by order and shape, fields of one type told apart, numbers rewritten on the wire (D54, D56)? | Step 3, the session after the next client update |
| Q12 | Which facts must the bot learn, beyond its state, for the criteria it cannot answer: quests, breed, alignment, achievements, the server's own codes; from which messages, and what does each code mean, measured (D71)? On doors, achievements would open the most, then `Sc` and `SC`, then quests (D76). | The large effort on criteria the owner noted, then as each step needs them |
| Q13 | What is a blueprint mod: who writes it, where it lives, what a node is over D3B's behaviors and state, how a graph runs as an activity, interrupted and resumed from the state, its settings, and which canvas in Avalonia (D117)? | Step 7 |
| Q14 | Does a client update renumber the maps' graphics, so that a skill learned on one (D138), which is never forgotten, would foretell another's maps wrongly: are the graphics learned kept as they are, checked, or forgotten at a new build? | The session after the next client update, with Q10 |
