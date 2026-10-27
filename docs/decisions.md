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

## Open questions

Settled questions leave this table, and the decision that settles one says so.

| Id | Question | Settled in |
| --- | --- | --- |
| Q1 | Does LuaCSharp hold up: sandboxing, async calls, errors, performance? | Step 7 |
| Q2 | How does the per-build translation work: rewriting field numbers on the wire, or decoding twice? | Step 3, session 3e |
| Q5 | Which fight formulas of Bubble emu (Dofus 2) still hold in Dofus 3? | Step 9 |
| Q6 | What is a plugin's UI part: an assembly loaded by the desktop app, with which contract? | Step 5 |
| Q7 | How are sessions captured and replayed for tests: in which format? The format is D37's; replays with injection remain. | Step 3, session 3f |
| Q9 | How does a bot act through its client's connection: which requests does D3B send, and how do the client and the bot stay consistent with what the server answers? | Step 3, session 3f |
