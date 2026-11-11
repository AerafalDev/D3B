# Now

_Updated 2026-11-11._

- **Step**: 1, the game data ([ROADMAP.md](../ROADMAP.md)), designed with the owner ([docs/data.md](data.md), ADR 0008).
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, the release workflow for six targets, release-please, the app's updates through the `updater-next` channel
  ([docs/updates.md](updates.md)). Decisions: [docs/adr/](adr/README.md) 0001 to 0008.
- **Not measured**: nothing yet. The first release build on macOS and Linux has not run.
- **In progress**: the extractor in `tools/extractor`, as a stack of PRs, each table with its Rust reader: the
  skeleton writing a manifest-only folder; the type-tree reader; bundles, texts and the world tables; the worlds and
  their tiles; the maps' cells; their elements; the world graph; the `.tar.gz` archive and `data:fetch` reading it.
  Until the last one, `scripts/fetch-data.ts` still expects a `.zip`.
- **Next**: the owner runs the extractor on the installed client and the status bar names the build; then the first
  feature of step 2, the relay, designed with the owner (`feature`).
- **Waiting on the owner**:
  - which Dofus 3 builds exist on macOS and Linux, and a machine to measure the relay on each.
- **Open**: the version number and the identifier (`0.10.0`, `dev.aerafal.d3b`) were taken by default, to confirm.
