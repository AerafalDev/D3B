# Now

_Updated 2026-11-11._

- **Step**: 1, the game data ([ROADMAP.md](../ROADMAP.md)), designed with the owner ([docs/data.md](data.md), ADR
  0008); step 0 is done but for its measure.
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, release-please running as the project's GitHub App, the release workflow for six targets, and the app's
  signed updates through the `updater-next` channel ([docs/updates.md](updates.md)). **v0.10.0**, the first
  pre-release, is published with installers for the six targets and no game data yet. Decisions:
  [docs/adr/](adr/README.md) 0001 to 0010.
- **Measured** by the owner, 2026-11-11, Windows x64: v0.10.0 installs per user, starts dark and in French, its status
  bar says "D3B 0.10.0" and "No game data", the close button quits, and a second launch brings the running window
  back.
- **Not measured**, by the owner:
  - v0.10.0 on macOS, Linux and ARM64: built by CI, run by nobody yet. The owner has none of them and no tester yet; the README says so, and a round starts when a tester comes ([docs/testing.md](testing.md));
  - the first update: with v0.10.0 installed, the next release is downloaded at start, the bar offers to restart,
    Restart installs it; on another try, closing installs it.
- **In progress**: the extractor in `tools/extractor`, as a stack of PRs, each table with its Rust reader. Done: the
  .NET project, checked by CI and `bun run check`, which finds the client, reads its build and writes a manifest-only
  folder the app accepts; reading the client's bundles and data tables through their type trees, its texts in five
  languages, and the names of its flag enums; the map positions, sub-areas, areas and zaaps, which the app reads at
  start; the worlds of the map and their 1,250 tiles; the 17,623 maps, their cells and elements; the world graph, checked against the maps. To come: the `.tar.gz` archive
  and `data:fetch` reading it. Until the last one, `scripts/fetch-data.ts` still expects a `.zip`.
- **Next**: the owner runs the extractor on the installed client and the status bar names the build; then the first
  feature of step 2, the relay, designed with the owner (`feature`).
- **Waiting on the owner**:
  - which Dofus 3 builds exist on macOS and Linux, and a machine to measure the relay on each.
- **Open**: the identifier `dev.aerafal.d3b` was taken by default, to confirm before more people install the app.
