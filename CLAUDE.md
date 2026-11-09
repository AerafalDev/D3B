# D3B

An open-source bot for Dofus 3 (Unity), in .NET 10 on Windows, assembled from modules.
The repository is the only memory between sessions: never assume an earlier session is remembered.

## Read first

1. This file.
2. [docs/status.md](docs/status.md): where the project stands, what is not measured, what comes next, the known debt.
3. [docs/decisions-index.md](docs/decisions-index.md): one line per decision. Open the entries of [docs/decisions.md](docs/decisions.md)
   about the area you work on, plus the brief and the open questions at its ends; the file is about 700 KB, never read it whole.
4. [docs/architecture.md](docs/architecture.md): how the code is built, section by section: read the ones you touch.
5. [docs/roadmap.md](docs/roadmap.md): the steps and the next one.
6. [docs/client.md](docs/client.md), when the task touches the official client's content: what it holds, measured. Grep its headings.
7. The actual state of the repository (`git log`, `git status`, the code).

## Working agreement

- The lead session acts as lead developer and architect, responsible for the project's overall coherence.
- Talk to the project owner in French. Everything in the repository is in English.
- **Decisions.** Settle local, reversible choices alone. For an important or hard-to-reverse one: analyse, research,
  compare, recommend, then ask the owner, one decision at a time. Say plainly when an idea is bad, premature or a
  source of debt. Record each decision with `/decision`: an entry in `docs/decisions.md` and its line in
  `docs/decisions-index.md`. Entries are append-only: a later entry supersedes an earlier one.
- **Done** means code, the tests it needs (for `src/` only, since scripts have none, D10), docs, and specific
  checks when relevant, with every check green. The docs are the roadmap, `docs/status.md`, the index and the
  architecture sections the change touches.
- **Scope.** One roadmap step per session, or part of one. Every new large task starts in a new session. No
  refactoring outside the task without a reason.
- **Delivery.** One branch and one pull request per session. Never push, open a pull request or merge without an
  explicit go from the owner: a hook asks again at each. `/ship` prepares the branch, `/review` has a fresh agent read it.
- **Closing a session.** `/close-session`: report what was done, the decisions taken, the remaining risks, the project's
  state, the recommended next task, and a prompt for the next session, ready to copy and paste.

## Never

- What D2 puts out of scope: evading Ankama's detection, mass account creation, subscription or payment automation,
  disabling Ankama Shield, one proxy per account to hide multi-accounting.
- Copying the references in `vendor/`: they are read for ideas (D3). The only exception is code the owner wrote in BillBot.
- Committing anything extracted from the official client: no assets, data, dumps, per-build mappings or packs. Packs and
  mappings ship as release artifacts (D10, D11, D24). The readable `.proto` files and the pinned pairs are ours.
- Putting an AI attribution in a commit or a pull request (a `Co-Authored-By` trailer, a "Generated with" line): a hook refuses it.
- Showing a capture's content: it holds the account's login ticket. Describe it with `scripts/capture.cs`.

## Conventions

- Build and configuration files (`.csproj`, `.props`, `.targets`, `global.json`) carry no comments. Their rationale
  goes in the docs.
- Code comments explain why a constraint exists, not what the code does.
- Tools (D10): a file-based app in `scripts/` whose entry file starts with `#!/usr/bin/env dotnet` (CA2266), with its
  code pulled in through `#:include` from a lowercase folder (`scripts/unity/` for what several tools share),
  `#:package` without a version (central package management), and `#:property PublishAot=false` when a package is not
  AOT-compatible. Every script file is listed in the solution, so that it shows in the IDE. `/new-tool` has the steps.
- `CONTRIBUTING.md` restates these conventions and the C# style below for outside contributors, and the README the
  commands below for users: a change here changes them too (D204).

## C# style

These rules apply to `src/`, `tests/`, `scripts/` and `.claude/hooks/`. The build enforces the first two;
the rest is yours to keep.

- Methods, constructors, operators and local functions have block bodies, never `=>`. Properties, indexers and
  accessors may use `=>` (IDE0021 to IDE0024, IDE0061).
- Test methods are PascalCase sentences without underscores, such as `AModuleTypeIsBuiltOnce` (CA1707).
- One type per file, and one folder per namespace.
- Members go in this order: constants, static fields, fields, static properties, properties and indexers, events, static
  constructor, constructors, static methods, methods, operators, nested types. No primary constructors.

## Commands

```sh
dotnet build                               # warnings are errors; about 20 s
dotnet test                                # every test project, on Microsoft.Testing.Platform
dotnet format --verify-no-changes          # CI checks it
dotnet run --project src/D3B.App           # the desktop app, which runs the bots' host in its process (D91); closing it disconnects the clients
dotnet run --project src/D3B.Host          # the same host without the app; needs the installed build's pack, schema and mapping (D172)
dotnet run --file scripts/<tool>.cs -- --help   # the tools of scripts/, each described by its own help
```

The README has the rest: the host's options, the installer, the local tools. Two tools change things: `scripts/release.cs`
publishes only with `--publish`, and `scripts/client.cs -- restore` gives the client Ankama's configuration back after a
crash. The shell is Git Bash on Windows.

## The project's Claude setup (D398)

- Skills in `.claude/skills/`: `/decision`, `/ship`, `/review`, `/close-session`, `/new-tool`, and, loaded when the task
  fits, `csharp`, `test-first`, `studio-ui`, `git`, `read-client`, `diagnosing-bugs`, and `csharp-refactoring`. Agents: `reviewer`, which reads a diff fresh, and `explorer`,
  which studies the client and `vendor/` and writes only under `artifacts/studies/`.
- Hooks, C# apps in `.claude/hooks/` tested in `tests/D3B.Hooks.Tests`: pushes, `gh` writes and `git clean` ask first;
  pushes to `main`, forced pushes, `git reset --hard` and AI attributions in commits are refused, so are writes of what the client or a machine
  produced; the C# written is formatted; a turn that left changed code ends on a green `dotnet build`.
- Once per machine, copy `.claude/settings.local.example.json` to `.claude/settings.local.json`: it turns off the plugins
  and skills claude.ai syncs into every session.
