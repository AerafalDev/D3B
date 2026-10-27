# D3B

An open-source bot for Dofus 3 (Unity), in .NET 10 on Windows, assembled from modules.
The repository is the only memory between sessions: never assume an earlier session is remembered.

## Read first

1. This file.
2. [docs/decisions.md](docs/decisions.md): the brief, every decision and the open questions.
3. [docs/architecture.md](docs/architecture.md): the target architecture and what exists.
4. [docs/roadmap.md](docs/roadmap.md): the steps and the next one.
5. The actual state of the repository (`git log`, `git status`, the code).

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
  Packs and mappings ship as release artifacts (D10, D11). The readable `.proto` files and the pinned pairs are ours
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

None yet: the solution is created at roadmap step 1.
