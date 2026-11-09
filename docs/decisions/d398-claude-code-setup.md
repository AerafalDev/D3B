# Claude Code setup (D398)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D398: The project's Claude Code setup is versioned, with hooks in C# and tests

*2026-11-09, outside the roadmap's steps, a session of its own as the owner asked it, modelled on the Dofus web client's `.claude/` (read only). Builds D2, D3, D10, D11, D17, D24, D204.*

- **Found.** D3B had no `.claude/` but its ignored worktrees. `CLAUDE.md` told sessions to use skills (`dotnet`, `git-workflow`,
  `reverse-engineer-anything`, `diagnosing-bugs`) that the repository did not hold and the owner's machine did not have: no personal
  skill, no user plugin, no MCP server. The model's hooks are TypeScript on Bun and its skills `vite`, `vitest` and the web client's
  ports, none of which D3B has; its process (a spec for each step, the owner's standing go to push and merge) is not D3B's, where
  every push, pull request and merge waits for an explicit go. So the setup was adapted, not copied.
- **Hooks are C# file-based apps** in `.claude/hooks/`, declared in `.claude/settings.json` as `dotnet run --file`. Each entry file
  only reads Claude Code's JSON and reports; the decisions are in `.claude/hooks/shared/`, pulled in by `#:include` (D10), and
  `tests/D3B.Hooks.Tests` compiles the same files, so that what is tested is what ships. D10 says the scripts have no tests; these
  guard rules instead of reading the client, so they have, and they follow D10's file conventions otherwise: they are listed in
  `D3B.slnx` and CI builds each one. Measured on the owner's machine: `dotnet run --file` of a trivial app 0.19 s warm and 2.7 s the
  first time after an edit; `dotnet format whitespace <csproj> --include <file>` 1.8 s; `dotnet build` of the solution 17 s, of
  `D3B.Core` 1.4 s.
- **The five.** `guard-git` (Bash and PowerShell): refuses a push to `main`, a forced or wholesale push and `git reset --hard`;
  refuses an AI attribution (a `Co-Authored-By` naming Claude, a "Generated with" line) in a commit or a pull request, as the owner asked; asks before any other push, before `gh` publishes or merges (`pr`, `issue`, `release` and `repo` writes), before `git clean -f`
  and `git branch -D`. `guard-writes`: refuses a write of what the client or a machine produces (`.pack`, `.binpb`, `.d3bcap`,
  `.db`, `.bundle`, `global-metadata.dat`), of `.proto` text outside `src/D3B.Protocol/` and `tests/`, and of `vendor/`;
  `artifacts/` is open. `format-file`: after a write, `dotnet format whitespace` of the file's project. `check-turn` (Stop):
  `dotnet build` when the turn left changed code, again only when a file changed since the last build (a fingerprint of the
  files and a green or red stamp in `artifacts/claude/`), so that Claude's fix is built and a repeated turn is not; a turn
  that was told the build fails and changed nothing is let go; and a build whose only errors are files the app running
  from this checkout holds does not fail the turn. `guard-studies`: the explorer agent writes only under `artifacts/studies/`, its shell only reads, and it runs
  `scripts/inspect.cs`, `capture.cs` and `fights.cs` only.
- **A safety net, not a boundary.** A call wrapped in `bash -c "..."` is not seen; a heredoc's body is text, not commands. `CLAUDE.md` still says the rules; the hooks hold
  those with no exception.
- **Skills** (`.claude/skills/`): `/decision`, `/ship` (stops before the push and asks), `/review`, `/close-session` (the report
  and the prompt `CLAUDE.md` asks for), `/new-tool` (the listing in `D3B.slnx` and the CI step are easy to forget), and, loaded when
  the task fits: `csharp` (how the source writes types, null, async, time, logging and collections, counted on 914 files),
  `test-first` (the loop, and the tests as the repository writes them), `studio-ui` (the studio's look, scale, texts in two
  languages, view models and headless tests, from the "Desktop app" section), `git` (the history's own habits: Conventional
  Commits, `<type>/<name>` branches, merge commits, staging by name, and no AI attribution, which the Git guard also refuses),
  `read-client` (the tools, and what never reaches the repository), `diagnosing-bugs` and `csharp-refactoring` and
  `benchmarks`, taken from mattpocock/skills at `4ae2317` and from dotnet/skills at `96c3d68` (MIT, each with its `SOURCE.md`)
  and turned to D3B: a replayed capture, a headless Avalonia test, never a login ticket; the generated code and the stored
  names a refactoring must not break; the repository's benchmark projects. **Agents**: `reviewer` (read-only; the never-ever
  list, the tests, the docs) and `explorer` (the client and `vendor/`, writing only the ignored studies).
- **Settings.** `autoMemoryEnabled` off, since the repository is the only memory; `dotnet build`, `test`, `format` and Git's
  reads allowed; reading a `.d3bcap` denied, since it holds a login ticket. The plugins and skills claude.ai syncs into every
  session cost context for nothing here: `.claude/settings.local.example.json` turns them off once per machine, as Claude Code
  ignores that setting in a versioned file.
- **`CLAUDE.md`** went from 99 to 92 lines: the commands' long comments (the README and each tool's `--help` say them) and the
  closing procedure (now `/close-session`) left; a "Never" section and the setup came in; the C# style says which rules the build
  already enforces.
- **Not done.** The `rea` MCP server and its skill are not vendored: `read-client` mentions it, and the owner says whether it is used.
  The model's environment audit is not ported. Skills of `dotnet/skills` worth a later look: `csharp-scripts`, `run-tests`,
  `test-anti-patterns`, `microbenchmarking`, `csharp-refactoring`. The hooks were exercised by hand with Claude Code's input
  shape, entry file by entry file, and tested; the write guard and the Git guard were seen refusing inside this session, the
  "ask" answers and the Stop hook's block not yet. A fresh agent reviewed the branch before its commit: it found the write
  guard refusing the `Captures/` folders of the code, `/review` skipping uncommitted work, and the Stop hook not rebuilding a
  fix, all corrected.
- **Rejected.**
  - The model's hooks in TypeScript: a second toolchain in a .NET repository.
  - Shell scripts: Git Bash and PowerShell both run here, and shell scripts have no tests.
  - Permission rules alone: they match prefixes, so `git -C x push` passes, and they cannot read a push's target.
  - `dotnet/skills` as plugins in the project's settings: a plugin loads every skill's description (`dotnet-test` has 22), and its
    install prompt through `extraKnownMarketplaces` has been reported not to appear.
  - The model's `spec`, `retro`, `hand-over`, `port-component`, `port-effect`, `vite` and `vitest`: a process or a stack D3B does not
    have.
