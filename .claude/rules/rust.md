---
paths:
  - "src-tauri/**/*.rs"
  - "src-tauri/Cargo.toml"
---

# Rust

- **Edition 2024, toolchain pinned** in `rust-toolchain.toml`; `cargo clippy --all-targets -- -D warnings` with
  `pedantic` on (the lints are in `Cargo.toml`). Silence a lint only with `#[expect(lint, reason = "...")]`, never
  `allow` without a reason. No `unsafe` (forbidden).
- **Errors**: one `thiserror` enum per domain for what fails in normal use, with the context a user or a log needs
  (the path, the build, the message). No `anyhow` in the domains; `unwrap`/`expect` only in tests, or with a comment
  saying why it cannot fail.
- **Domains are plain Rust**: `data`, `relay`, `protocol`, `game`, `bot`, `storage` do not import `tauri`. Time,
  randomness and I/O come in as parameters or traits, so tests drive them (a fixed clock, a seeded source, bytes).
- **Async** is Tokio. One task per long-lived actor (one per relay connection, one per bot), fed by channels; no shared
  `Mutex` across `.await`. Every task is cancellable and its handle is kept.
- **Hot paths** (the relay's frames, the fight search): no allocation per frame you can avoid; reuse buffers
  (`bytes::BytesMut`), bound every length read from the wire before reading it.
- **Visibility**: `pub(crate)` by default; `pub` only for what `ipc` or tests need. One subject per file, at most 10
  code files per folder; a folder's `mod.rs` says in its doc comment what the domain does.
- **Docs**: every public item has a doc comment saying what and why. A behaviour taken from the official client names
  its provenance: the build, and the capture or the study (`verified` or `inferred`).
- A new crate dependency goes through the `dependency` skill.
