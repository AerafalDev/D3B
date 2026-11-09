# Tidy-up (D399 to D401)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D399: D3B.Bot stays one project and is cut by files, and the six files over 900 lines are split

*2026-11-09, outside the roadmap's steps, a session of its own as the owner asked it ("a large refactoring and tidy-up session", `docs/status.md`). Builds D393, D5, D6.*

- **Found.** `D3B.Bot` is 17,900 lines in 224 files. Its core (`Runtime`, `Activities`, `Banks`, `Movement`, `Journal`,
  `Characters`) refers to itself in both directions, so no folder of it can leave without untangling it first. `Harvesting`
  (3,000 lines) and `Fighting` (1,700) are leaves: nothing in the core uses them but `DefaultModules`, and 201 of its top-level
  types are already public. `Fighting` reaches into `HarvestActivity.FoodTypes`, food being no business of harvesting. Six files
  have more than 900 lines, not the four `status.md` named: `FightJudge` 1,357, `RouteEditorViewModel` 1,238, `AssetsViewModel`
  1,196, `HarvestActivity` 970, `Imagination` 955 and `WorldMapView` 930; `BotActor` has 807.
- **Made.** `D3B.Bot` stays one project and no folder of it becomes one: the better cut is in the files. The six are split along
  their seams, and so are `BotActor` (the inbox and the expectations) and the food types, which move to `Inventory`. One move per
  commit, the tests run between moves. `AssetsViewModel` and `WorldMapView` get tests of the current behavior first, their cover
  being the thinnest. `FightJudge` and `Imagination` come last, the AI's brain: the `Tactics` tests hold them and, where their
  output is deterministic, `scripts/fights/CastBench.cs` and `TurnBench.cs` are compared before and after. `RouteEditorViewModel`
  and `WorldMapView` wait for the fix of the route drawing bug, which sits in the same files.
- **Rejected.** `D3B.Bot.Harvesting` and `D3B.Bot.Fighting` as projects of their own: a gain of about 4,700 lines out of the
  core against a new surface to decide (`InternalsVisibleTo`, the plugins' seam of D5 and D6, the tests' references) and the owner
  wanting no new project. Layers by namespace guarded by an architecture test: the owner chose the files only, and the debt of the
  core's cycle stays noted in `status.md`.

## D400: BenchmarkDotNet is dropped

*2026-11-09, the same session, the owner's choice. Supersedes the benchmark projects and the `benchmarks` skill of D398.*

- **Found.** Two projects, `benchmarks/D3B.Engine.Benchmarks` and `benchmarks/D3B.Network.Benchmarks`, 539 lines, referenced by
  nothing in `src/`. Four scripts in `scripts/fights/` (`CastBench`, `TurnBench`, `HuntBench`, `LiveBench`) carry "bench" in their
  names but are not BenchmarkDotNet: they replay captured fights to judge the AI's decisions.
- **Made.** The two projects, the `benchmarks` skill and every mention of them (`D3B.slnx`, `Directory.Packages.props`,
  `CLAUDE.md`, `CONTRIBUTING.md`, the docs) are removed, and "Done" no longer asks for benchmarks. The fight scripts stay, and so do
  `GroupJudge.OddsOf`, `Beats`, `Winnable` and `Share` that `HuntBench` needs (D387).
- **Rejected.** Removing the fight scripts too: they measure the AI's quality, not its speed, and the owner kept them. Keeping the
  projects without the demand for numbers: dead projects the build still pays for.

## D401: `decisions.md` is cut into files by group of the index

*2026-11-09, the same session, the owner's choice. Builds D204; the entries stay append-only.*

- **Found.** `docs/decisions.md` is 737 KB for 397 entries (median 1.5 KB, the longest 9 KB), and about 1,600 links in the docs,
  the skills, the agents and `CLAUDE.md` point at `decisions.md#<anchor>`.
- **Made.** `docs/decisions/` holds one file per group of the index, any larger than about 100 KB cut again; the Brief and the
  Open questions each have a file. The entries are the same word for word, which a check proves; a script rewrites the links and a
  link checker confirms every anchor still resolves. The index keeps its shape. `/decision` and `CLAUDE.md` say to append to the
  last file. `docs/client.md`'s "Connection" section (131 KB of 194) leaves for a file of its own, `docs/architecture.md`'s "Bot
  run time" keeps its present state and loses the history of sessions, and `status.md` counts the debt right.
- **Rejected.** Condensing the old or superseded entries: it breaks the append-only rule, loses the reason, which is the file's
  point, and cannot be checked. Leaving the file whole and reading it by pieces: what the sessions did until now, and what costs
  them.
