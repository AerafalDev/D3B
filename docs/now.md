# Now

_Updated 2026-11-11._

- **Step**: 1, the game data ([ROADMAP.md](../ROADMAP.md)), designed with the owner ([docs/data.md](data.md), ADR
  0008); step 0 is done but for its measure.
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, release-please running as the project's GitHub App, the release workflow for six targets, and the app's
  signed updates through the `updater-next` channel ([docs/updates.md](updates.md)). **v0.10.0**, the first
  pre-release, is published with installers for the six targets and no game data yet. Decisions:
  [docs/adr/](adr/README.md) 0001 to 0008.
- **Not measured**, by the owner:
  - v0.10.0 installs and starts on each OS at hand (Windows x64 first): the window opens, the status bar says
    "No game data", closing works;
  - the first update: with v0.10.0 installed, the next release is downloaded at start, the bar offers to restart,
    Restart installs it; on another try, closing installs it.
- **In progress**: the extractor in `tools/extractor`, as a stack of PRs, each table with its Rust reader: the
  skeleton writing a manifest-only folder; the type-tree reader; bundles, texts and the world tables; the worlds and
  their tiles; the maps' cells; their elements; the world graph; the `.tar.gz` archive and `data:fetch` reading it.
  Until the last one, `scripts/fetch-data.ts` still expects a `.zip`. The skeleton adds the .NET 10 SDK to
  `bun run check` and to the prerequisites (README, CONTRIBUTING, `.claude/rules/ci.md`).
- **Next**: the owner runs the extractor on the installed client and the status bar names the build; then the first
  feature of step 2, the relay, designed with the owner (`feature`).
- **Waiting on the owner**:
  - which Dofus 3 builds exist on macOS and Linux, and a machine to measure the relay on each.
- **Open**: the identifier `dev.aerafal.d3b` was taken by default, to confirm before more people install the app.
