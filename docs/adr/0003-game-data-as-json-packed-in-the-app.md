# 0003. Game data as JSON from a .NET extractor, packed in the app

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

The bot needs the game's data (maps, world graph, items, spells, monsters, texts, world map tiles) for the client
build it plays. The C# version extracted it with .NET-only libraries (AssetsTools.NET, Cpp2IL) into a MemoryPack
format no other language reads, about 105 MB with the tiles. Ankama's data must never be committed.

## Decision

- **A .NET extractor**, the C# tools cleaned up into one maintainer-side tool in `tools/extractor`, writes one folder
  per client build: `manifest.json` (format, build, tables with their SHA-256) and one JSON file per table, tiles
  included.
- **It ships as a release asset** `client-<build>.zip`, published on the owner's go and never replaced.
- **It is packed in the app**, everything included (the tiles too): the release workflow downloads the build named in
  `data-build.txt` into `data/`, which Tauri bundles as a resource. The app works offline.

## Rejected

- Rewriting the extraction in Rust: weeks before any data, for a tool only the maintainer runs.
- A binary pack (zstd, bincode): smaller, but the owner prefers JSON, readable and diffable. Compressing it remains
  possible if the installer's size becomes a problem, measured first.
- Downloading the tiles on demand: a lighter installer, but online and more code.

## Consequences

Each version of the app carries one client build's data; a new build needs a new data release, then an app release.
The installer is large (about 100 MB of data). See `docs/data.md`.
