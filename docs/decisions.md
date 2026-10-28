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
- Every feature is a module, assembled as in Bevy. External .NET plugins add modules.
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
| `metiers` | The owner's | Lua scripts the owner wrote for Snowbot, levelling each profession from 1 to 200. They are the acceptance test of the Lua API (D12). |

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

*2026-10-27*

An external plugin is a folder under `plugins/`. Each loads into its own `AssemblyLoadContext`, which shares the host's
assemblies, as Palforge's `PluginLoadContext`, but is not collectible. A plugin declares its identity with an
attribute. A `dotnet new` template creates one.

- **Why.** Unloading constrains every type a plugin touches and is rarely needed: updating a plugin restarts the host.
- **Rejected.**
  - Collectible contexts, as in Palforge.
  - Modules compiled into the application only: nobody could add one from outside.
  - NuGet packages or a catalogue in the UI: more work than a folder needs at this stage.

## D7: A headless host runs the bots, the desktop app is its client

*2026-10-27*

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

*2026-10-27*

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
applies.*

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

*2026-10-27, step 1*

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

*2026-10-27, step 1*

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

*2026-10-27, step 1, chosen by the owner*

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

*2026-10-28, step 4, session 4a*

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

## Open questions

Settled questions leave this table, and the decision that settles one says so.

| Id | Question | Settled in |
| --- | --- | --- |
| Q1 | Does LuaCSharp hold up: sandboxing, async calls, errors, performance? | Step 7 |
| Q5 | Which fight formulas of Bubble emu (Dofus 2) still hold in Dofus 3? | Step 9 |
| Q6 | What is a plugin's UI part: an assembly loaded by the desktop app, with which contract? | Step 5 |
| Q10 | Does every client build shuffle and renumber the game's fields, and how is a build's game mapped from the previous build's: messages by order and shape, fields of one type told apart, numbers rewritten on the wire (D54, D56)? | Step 3, the session after the next client update |
| Q11 | What does a plugin see of the bots' run time: `D3B.Bot` packed beside `D3B.Core`, or the bot's contracts (state, behaviors, observers) moved into `D3B.Core`, which plugins compile against (D22, D67)? | Before the first milestone publishes the package (step 6) |
| Q12 | Which facts must the bot learn, beyond its state, for the criteria it cannot answer: quests, breed, alignment, achievements, the server's own codes; from which messages, and what does each code mean, measured (D71)? On doors, achievements would open the most, then `Sc` and `SC`, then quests (D76). | The large effort on criteria the owner noted, then as each step needs them |
