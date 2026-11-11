# 0004. A typed IPC generated from Rust with tauri-specta

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

Every call between the webview and the back end crosses a JSON boundary. Types written by hand on both sides drift.

## Decision

- Commands and events are declared once, in Rust, with `#[specta::specta]`, and registered in `ipc::builder()`.
- `src/bindings.ts` is generated from them by the `bindings_are_current` test (`cargo test`); CI fails when it is stale,
  and the file guard refuses editing it by hand.
- `specta` and `tauri-specta` are pinned to an exact release candidate (`=2.0.0-rc.28`) and raised on purpose.

## Rejected

- ts-rs: stable, but types only; every `invoke` would still be hand-written.
- Hand-written types: no generation step, but they drift.

## Consequences

The TypeScript side never declares an IPC type. Raising the release candidate may need small changes to the export
call.
