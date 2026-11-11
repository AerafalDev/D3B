---
name: data
description: Extract the game data of the installed client build into JSON, check it, and publish it as the client-<build> release asset on the owner's go. Use when Ankama updates the client or the data format changes.
disable-model-invocation: true
argument-hint: "[client build]"
---

The data pipeline is in `docs/data.md`. Nothing here is ever committed.

1. **Which build**: read the installed client's version (`Dofus_Data/StreamingAssets/version` under the client's
   folder, `--client`, `DOFUS_CLIENT` or the default in `docs/data.md`).
2. **Extract**: run the extractor with its `--help` first (`dotnet run --project tools/extractor/src -- --help`); it
   writes `artifacts/data/<build>/` with `manifest.json`, one JSON file per table and the tiles, and its archive
   `artifacts/data/client-<build>.tar.gz`. Until the extractor is complete (`docs/now.md`), say what it does not
   extract yet.
3. **Check**: the manifest's `format` equals `data::FORMAT`, every table's SHA-256 matches, and the app reads it:
   copy the folder into `data/` and run `bun run app`; the status bar names the build.
4. **Publish, only on the owner's go**: when the C# version already made a `client-<build>` release, add the archive
   to it (`gh release upload client-<build> artifacts/data/client-<build>.tar.gz`): one release per build, the
   rewrite replacing the C# version. Otherwise `gh release create client-<build> artifacts/data/client-<build>.tar.gz
   --prerelease --title "Game data <build>" --notes "..."`. A published asset is never replaced: a fix is a new format.
5. **Use it**: write the build in `.github/game-data.json` (`"build": "<build>"`) in a PR, so the next release packs it.
