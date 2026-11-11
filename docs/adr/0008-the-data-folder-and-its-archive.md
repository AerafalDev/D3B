# 0008. The data folder holds the world first, in five languages, and ships as a `.tar.gz`

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner
- Supersedes 0003

## Context

ADR 0003 decided that the game data is JSON, written by a .NET extractor and packed in the app. Designing step 1 showed
three things it left open or got wrong: GNU tar, on Ubuntu and in Git Bash, cannot read the `.zip` it named; the C#
pack held tables no code of the rewrite reads before step 6; and how texts and the 17,623 maps are laid out.

## Decision

- **Everything 0003 decided holds**: a .NET extractor in `tools/extractor`, kept on the maintainer's side, writes one
  folder per client build (`manifest.json` with every file's SHA-256, then JSON); the folder ships as a release asset
  published on the owner's go and never replaced; the release workflow packs it in the app, world map tiles included.
- **The archive is `client-<build>.tar.gz`**, written by the extractor with .NET's own `System.Formats.Tar`: every
  `tar` reads it, on the three OSes.
- **The first cut is the world**: map positions, sub-areas, areas, zaaps, the worlds and their tiles, the maps' cells
  and elements, the world graph. Items, monsters, spells and the other tables come with the step that reads them.
- **Texts are inline**, in the five languages the client holds: `{"fr", "en", "de", "es", "pt"}` in each record.
- **Maps are split in 100 files** by the last two digits of their id, `maps/<id % 100>.json`.
- **The extractor is a `.csproj` with a test project**, on System.CommandLine, built and tested by CI and by
  `bun run check`. It extracts everything again at each run.

## Rejected

- Keeping `.zip`: `fetch-data.ts` would need another tool per OS.
- The whole C# pack now: over 10 MB of shapes no code reads before step 6.
- Text ids and per-language tables: a lookup for every name; French and English only: the owner wants the five.
- One file per map (about 19,000 files in the installers), one per sub-area (a catch-all for maps without one), or
  one `maps.json` (about 90 MB parsed at start).
- A file-based app: no real test project, when the extractor must be tested in CI without the client.

## Consequences

`bun run check` needs the .NET 10 SDK. The folder's size grows with the five languages; it is measured on the first
extraction. A table added later is a new format (`data::FORMAT` and the extractor's together).
