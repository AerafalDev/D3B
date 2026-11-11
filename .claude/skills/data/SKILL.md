---
name: data
description: Extract the game data of the installed client build into JSON, check it, and publish it as the client-<build> release asset on the owner's go. Use when Ankama updates the client or the data format changes.
disable-model-invocation: true
argument-hint: "[client build]"
---

The data pipeline is in `docs/data.md`. Nothing here is ever committed.

1. **Which build**: read the installed client's version (`Dofus_Data/StreamingAssets/version` under the client's
   folder, `DOFUS_CLIENT` or the default per OS in `docs/data.md`).
2. **Extract**: run the extractor (`tools/extractor`, a .NET tool) with its `--help` first; it writes
   `artifacts/data/<build>/` with `manifest.json` and one JSON file per table. Until the extractor is ported, say so
   and stop.
3. **Check**: the manifest's `format` equals `data::FORMAT`, every table's SHA-256 matches, and the app reads it:
   copy the folder into `data/` and run `bun run app`; the status bar names the build.
4. **Publish, only on the owner's go**: zip the folder's content as `client-<build>.zip`, then
   `gh release create client-<build> client-<build>.zip --prerelease --title "Game data <build>" --notes "..."`. A
   published asset is never replaced: a fix is a new format.
5. **Use it**: write the build in `.github/game-data.json` (`"build": "<build>"`) in a PR, so the next release packs it.
