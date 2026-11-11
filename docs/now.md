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
    Restart installs it; on another try, closing installs it. It starts with a new identifier (ADR 0009), so its data
    folders start empty;
  - **step 1, the extractor**, on the installed client (Windows). Measured by the owner on 2026-11-11, Windows x64:
    items 1, 2 (every count as below, in 27.8 s) and 4 pass. Not measured yet: items 3 and 5.
    1. `dotnet run --project tools/extractor/src -- --help` lists `--client` and `--output`;
    2. `dotnet run --project tools/extractor/src` prints build 3.7.13.17 and 15,470 map positions, 569 sub-areas, 70
       areas, 63 zaaps, 40 worlds, 161 zooms, 1,250 tiles, 17,623 maps, 46,490 elements, 11,331 vertices, 30,917
       edges, then exits 0 in about 30 s, writing `artifacts/data/3.7.13.17/` and `artifacts/data/client-3.7.13.17.tar.gz`;
    3. a second run writes the same `manifest.json`;
    4. `tar -xzf artifacts/data/client-3.7.13.17.tar.gz -C data`, then `bun run app`: the status bar names 3.7.13.17,
       with no error in its tooltip;
    5. with `"format": 2` in `data/manifest.json`, the status bar says there is no data and its tooltip names the format.
- **Done in code, step 1**: `tools/extractor` reads the client's bundles, data tables, texts in five languages and flag
  enums, and writes the map positions, sub-areas, areas, zaaps, worlds and their tiles, maps, and the world graph,
  checked against one another, with the `client-<build>.tar.gz` that `data:fetch` reads; the app reads it all at start
  but the maps, which it reads one file at a time.
- **Debt**, from the review of step 1: the readers of maps, of the world graph and of the tiles are tested on the
  installed client only (skipped in CI); hand-built trees (`TypeTree.Build`) would test their failures in CI too.
- **Next**: once step 1 is measured, publish `client-3.7.13.17` on the owner's go (`data` skill), then the first
  feature of step 2, the relay, designed with the owner (`feature`).
- **Waiting on testers**: Dofus 3 has a native client on macOS and on Linux (the owner, 2026-11-11); where it is
  installed, whether its data is Windows', and the relay on each are measured by the first testers there.
