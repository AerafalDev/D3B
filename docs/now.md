# Now

_Updated 2026-11-11._

- **Step**: 0, the foundations ([ROADMAP.md](../ROADMAP.md)).
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, the release workflow for six targets, release-please, the app's updates through the `updater-next` channel
  ([docs/updates.md](updates.md)). Decisions: [docs/adr/](adr/README.md) 0001 to 0006.
- **Not measured**: nothing yet. The first release build on macOS and Linux has not run.
- **Next**: the .NET extractor, cleaned up in `tools/extractor`, writing the JSON data folder (step 1); then the
  first feature of step 2, the relay, designed with the owner (`feature`).
- **Waiting on the owner**:
  - which Dofus 3 builds exist on macOS and Linux, and a machine to measure the relay on each.
- **Open**: the version number and the identifier (`0.10.0`, `dev.aerafal.d3b`) were taken by default, to confirm.
