# D3B

An open-source bot for Dofus 3 (Unity), in .NET 10 on Windows, assembled from modules.
The repository is the only memory between sessions: never assume an earlier session is remembered.

## Read first

1. This file.
2. [docs/decisions.md](docs/decisions.md): the brief, every decision and the open questions.
3. [docs/architecture.md](docs/architecture.md): the target architecture and what exists.
4. [docs/roadmap.md](docs/roadmap.md): the steps and the next one.
5. [docs/client.md](docs/client.md), when the task touches the official client's content: what it holds, measured.
6. The actual state of the repository (`git log`, `git status`, the code).

## Working agreement

- The lead session acts as lead developer and architect, responsible for the project's overall coherence.
- Talk to the project owner in French. Everything in the repository is in English.
- **Decisions.** Settle local, reversible choices alone. For an important or hard-to-reverse one: analyse, research,
  compare, recommend, then ask the owner, one decision at a time. Say plainly when an idea is bad, premature or a
  source of debt. Record each decision in `docs/decisions.md`. Entries are append-only: a later entry supersedes an
  earlier one.
- **Done** means code, the tests it needs (for `src/` only, since scripts have none, D10), docs, and benchmarks or
  specific checks when relevant, with every check green.
- **Scope.** One roadmap step per session, or part of one. Every new large task starts in a new session. No
  refactoring outside the task without a reason.
- **Delivery.** One branch and one pull request per session. Never push, open a pull request or merge without an
  explicit go from the owner.
- **Closing a session.** Report what was done, the decisions taken, the remaining risks, the project's state, the
  recommended next task, and a prompt for the next session, ready to copy and paste.
- Use the installed skills when they help: the `dotnet` skills (C#, MSBuild, tests, refactoring), `git-workflow` for
  Git, `reverse-engineer-anything` for the official client and its protocol, and `diagnosing-bugs` for hard bugs.

## Conventions

- Never add what D2 puts out of scope: evading Ankama's detection, mass account creation, subscription or payment
  automation, disabling Ankama Shield, one proxy per account to hide multi-accounting.
- The references in `vendor/` are read for ideas, never copied (D3). The only exception is code the owner wrote
  in BillBot.
- Nothing extracted from the official client is ever committed: no assets, data, dumps, per-build mappings or packs.
  Packs and mappings ship as release artifacts (D10, D11, D24). The readable `.proto` files and the pinned pairs are ours
  and are committed.
- Build and configuration files (`.csproj`, `.props`, `.targets`, `global.json`) carry no comments. Their rationale
  goes in the docs.
- Code comments explain why a constraint exists, not what the code does.
- Tools (D10): a file-based app in `scripts/` whose entry file starts with `#!/usr/bin/env dotnet` (CA2266), with its
  code pulled in through `#:include` from a lowercase folder (`scripts/unity/` for what several tools share),
  `#:package` without a version (central package management), and `#:property PublishAot=false` when a package is not
  AOT-compatible. Every script file is listed in the solution, so that it shows in the IDE.

## C# style

These rules apply to `src/`, `tests/`, `benchmarks/` and `scripts/`.

- One type per file, and one folder per namespace.
- Methods, constructors, operators and local functions have block bodies, never `=>`. Properties, indexers and
  accessors may use `=>`. The build enforces it (IDE0021 to IDE0024, IDE0061).
- Members go in this order: constants, static fields, fields, static properties, properties and indexers, events, static
  constructor, constructors, static methods, methods, operators, nested types. No primary constructors.
- Test methods are PascalCase sentences without underscores, such as `AModuleTypeIsBuiltOnce`. The build enforces it
  (CA1707).

## Commands

```sh
dotnet build                               # warnings are errors; copies the sample plugin into the Core tests' plugins/
dotnet test                                # every test project, on Microsoft.Testing.Platform
dotnet format --verify-no-changes          # CI checks it
dotnet run --project src/D3B.Host          # runs until Ctrl+C; needs the installed build's pack, local or downloaded (D31, D32); --Plugins:Directory <dir> loads plugins from elsewhere
                                           # while it runs, clients started from the launcher go through its relay (D35); --Network:Captures:Enabled true records them
dotnet pack src/D3B.Core -o <feed>         # the package plugins compile against (D22)
dotnet new install templates/d3b-plugin    # then: dotnet new d3b-plugin -n <Name> --d3b-version <version>
dotnet run --file scripts/inspect.cs -- summary   # describes the installed client; --help lists the commands
dotnet run --file scripts/extract.cs       # writes the installed client's pack to %LOCALAPPDATA%\D3B\data; --help for options
dotnet run --file scripts/release.cs       # dry run of publishing that pack to release client-<version>; --publish does it (D33)
dotnet run --file scripts/client.cs        # which configuration the client reads, Ankama's or D3B's; restore gives it Ankama's back after a crash
dotnet run --file scripts/capture.cs       # describes the captures by structure, never content; --frames shows each frame's protobuf shape
                                           # --schema <file> reads them with a schema, field by field
dotnet run --file scripts/schema.cs        # writes the installed build's obfuscated protocol schema beside its pack; --proto <dir> as .proto text
dotnet run --file scripts/mapping.cs       # matches the readable .proto files with that schema, audits, writes the build's mapping beside it
dotnet run -c Release --project benchmarks/D3B.Network.Benchmarks   # BenchmarkDotNet; --filter '*' runs them all
```

CI also creates a plugin from the template and builds it against a freshly packed D3B.Core: see the "Check the plugin
template" step of `.github/workflows/ci.yml` to run it by hand.
