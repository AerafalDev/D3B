# data

The game data packed in the app: the JSON folder the extractor writes for one client build (`manifest.json` and one
file per table, world map tiles included). Everything here but this file is ignored by Git: the data is extracted from
Ankama's client and is never committed.

- **In a release**, the workflow downloads the `client-<build>` release asset into this folder before `tauri build`.
- **From source**, put a data folder here by hand, or run `bun run data:fetch <build>` (needs the GitHub CLI signed in).

Without it the app still starts, and says it has no data. See [docs/data.md](../docs/data.md).
