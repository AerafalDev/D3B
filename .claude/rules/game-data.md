---
paths:
  - "src-tauri/src/data/**"
  - "src-tauri/src/protocol/**"
  - "src-tauri/src/relay/**"
  - "tools/extractor/**"
  - "data/**"
  - "proto/**"
---

# Game data, protocol and the relay

- **Nothing from the official client is committed**: no extracted JSON, assets, bundles, `global-metadata.dat`,
  schemas, mappings or captures. They stay in `data/` and `artifacts/` (ignored) and ship as `client-<build>` release
  assets. The file guard refuses most of it; `git diff --stat` before a commit catches the rest.
- **What is ours**: the readable `.proto` files, the pins that pair them with a build's obfuscated names, the JSON
  shapes of `data/` (`docs/data.md`), hand-written fixtures.
- **The data folder** has a `manifest.json` with its `format`; a change of shape bumps `data::FORMAT` and the
  extractor's format together, in one PR.
- **The protocol** is read through a per-build mapping; a frame that is not understood passes through untouched, a
  mapped message that does not re-encode to the same bytes is not trusted, and an unknown build stops the bot with a
  clear message, never a guess.
- **The relay** never sends anything to the client of its own; it only inserts the bot's requests between the
  client's frames, and only requests that start an action.
- **Captures** hold the account's login ticket: describe them by structure, never print their content.
- Before coding a behaviour of the game, find its study (`study` skill): the C# version's measured docs
  (`docs/client.md`, `docs/client-game.md` on its `main`) are a source, verified against a capture when they decide.
