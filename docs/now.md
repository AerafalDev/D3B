# Now

_Updated 2026-11-11._

- **Step**: 0, the foundations ([ROADMAP.md](../ROADMAP.md)), done but for its measure; step 1 has started.
- **Done**: the rewrite starts on branch `next`, which will replace the C# version on `main`. The Tauri 2 + React 19
  shell, the typed IPC, the packed data's manifest, i18n FR/EN, the Claude setup (`CLAUDE.md`, `.claude/`), CI on
  Ubuntu, release-please running as the project's GitHub App, the release workflow for six targets, and the app's
  signed updates through the `updater-next` channel ([docs/updates.md](updates.md)). **v0.10.0**, the first
  pre-release, is published with installers for the six targets and no game data yet. Decisions:
  [docs/adr/](adr/README.md) 0001 to 0007.
- **Not measured**, by the owner:
  - v0.10.0 installs and starts on each OS at hand (Windows x64 first): the window opens, the status bar says
    "No game data", closing works;
  - the first update: with v0.10.0 installed, the next release is downloaded at start, the bar offers to restart,
    Restart installs it; on another try, closing installs it.
- **Next**: step 1, the .NET extractor writing the JSON data folder, in its own session (branch `feat/extractor`):
  design with the owner first.
- **Waiting on the owner**:
  - which Dofus 3 builds exist on macOS and Linux, and a machine to measure the relay on each.
- **Open**: the identifier `dev.aerafal.d3b` was taken by default, to confirm before more people install the app.
