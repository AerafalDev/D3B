---
paths:
  - "src-tauri/src/ipc/**"
  - "src-tauri/src/lib.rs"
  - "src-tauri/tauri*.conf.json"
  - "src-tauri/capabilities/**"
  - "src-tauri/build.rs"
  - "src/bindings.ts"
---

# Tauri 2

- **Commands are thin**: read the app's state, call a domain, return plain data. Every command and event is
  registered in `ipc::builder()` with `#[specta::specta]`; `cargo test` regenerates `src/bindings.ts` and CI fails
  when it is stale. Never hand-write an `invoke` or an IPC type in TypeScript.
- **Events** for what the back end pushes (a character's state, a frame for the inspector): typed with
  `tauri_specta::Event`, throttled at the source (no more than what the screen shows, about 30 per second).
- **Capabilities are minimal**: `capabilities/default.json` grants only what the interface uses today. A new plugin
  permission is named in its PR.
- **CSP stays strict** (`tauri.conf.json`): no remote origin, no `unsafe-eval`. Game assets are read through the asset
  protocol, scoped to `$RESOURCE/data/**`.
- **The packed data** is the resource `data/` (from `../data/`); read it through `app.path().resource_dir()`, never
  from a path relative to the executable.
- **Cross-platform**: paths through `app.path()` and `std::path`, never `%LOCALAPPDATA%` or `~` by hand. Something only
  one OS can do sits behind `#[cfg(target_os = "...")]` with the others' behaviour stated.
- Release-only settings go in `tauri.release.conf.json`; the updater's public key stays in `tauri.conf.json`, its
  private key only in GitHub's secrets.
- Windows test binaries get the Common Controls manifest from `build.rs`; do not remove it (`cargo test` dies with
  `STATUS_ENTRYPOINT_NOT_FOUND` without it).
