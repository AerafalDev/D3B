# 0002. One Rust crate, a folder per domain, at most 10 code files per folder

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

The C# version had ten projects with dependencies running both ways between some of them. A Cargo workspace of
crates was proposed; the owner preferred a single crate kept tidy.

## Decision

- One crate, `src-tauri`, with one folder per domain under `src-tauri/src/`: `data`, `ipc`, then `relay`, `protocol`,
  `game`, `bot`, `storage` as they come.
- `ipc` is the only folder that talks to the webview. The other domains do not import `tauri`, so they are tested as
  plain Rust.
- At most 10 code files per folder, tests and the registry components (`src/components/ui`, `src/components/flow`)
  apart, in Rust and TypeScript alike; `scripts/check-folders.ts` enforces it in CI.

## Rejected

- A workspace of crates per domain: stronger boundaries and parallel compilation, but more manifests and ceremony than
  the owner wants now. It stays possible later: a domain that does not import `tauri` moves into a crate unchanged.

## Consequences

Boundaries between domains are kept by review (`pub(crate)` by default, no `tauri` outside `ipc` and `lib.rs`), not
by the compiler.
