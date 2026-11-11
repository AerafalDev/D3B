# 0008. The data folder holds the world first, in five languages, and ships as a `.tar.gz`

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner
- Supersedes 0003

## Context

ADR 0003 made the game data JSON, from a .NET extractor, packed in the app. Designing step 1 showed what it left open
or got wrong: GNU tar (Ubuntu, Git Bash) cannot read the `.zip` it named; the C# pack held tables no code reads yet;
how texts and maps (17,623 on 3.7.13.17, `main:docs/client.md`) are laid out.

## Decision

- **What 0003 decided holds, but the archive's name**: a .NET extractor in `tools/extractor`, kept on the maintainer's side, writes one
  folder per client build (`manifest.json` with every file's SHA-256, then JSON); the folder ships as a release asset
  published on the owner's go and never replaced; the release workflow packs it in the app, world map tiles included.
- **The archive is `client-<build>.tar.gz`**, written by the extractor with .NET's own `System.Formats.Tar`: every
  `tar` reads it, on the three OSes.
- **The first cut is the world**: map positions, sub-areas, areas, zaaps, the worlds and their tiles, the maps' cells
  and elements, the world graph. Items, monsters, spells and the other tables come with the step that reads them.
- **Texts are inline**, in the five languages the client holds (`Content/I18n/*.bin`): `{"fr", "en", "de", "es",
  "pt"}` in each record.
- **Maps are split in 100 files** by the last two digits of their id, `maps/<id % 100>.json`.
- **The extractor is a `.csproj` with a test project**, on AssetsTools.NET, AssetRipper.TextureDecoder, SkiaSharp,
  System.CommandLine and xUnit v3, checked by CI and `bun run check`. Each run extracts everything again.

## Rejected

- Keeping `.zip`: `fetch-data.ts` would need another tool per OS. A file-based app: no real test project.
- The whole C# pack now: shapes no code reads yet (the spells alone were 10.24 MB in the C# pack).
- Text ids and per-language tables: a lookup for every name; French and English only: the owner wants the five.
- One file per map (about 17,600 in the installers), per sub-area (a catch-all for maps without one), or one
  `maps.json` (the C# pack's maps were 90.7 MB serialized, all parsed at start).

## Consequences

`bun run check` needs the .NET 10 SDK. The size the five languages add is measured on the first extraction. A table
added later is a new format (`data::FORMAT` and the extractor's together).
