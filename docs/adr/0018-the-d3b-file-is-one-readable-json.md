# 0018. The `.d3b` file is one readable JSON holding the blueprint's file

- Status: accepted
- Date: 2026-11-16
- Decided by: the owner

## Context

ADR 0015 shares a blueprint with the zones and routes it names as one `.d3b` file, imported after a preview. Files
shared on Discord stay in the wild for years: whatever shape they take now, every later D3B must read it or refuse it
with why.

## Decision

- **One JSON**, indented: `{"d3b": 1, "blueprint": {…}, "zones": [], "routes": []}`. `d3b` is the file's format,
  read first, so that a newer D3B's file is said to be one; `blueprint` is the blueprint's own file as `bot` writes it,
  with its `schema`, read and checked as a file of the folder is; a key unknown or given twice is refused.
- **Zones and routes** are empty lists until zones and routes of mine exist (step 3's PRs 20 and 21), which fill them
  with those the blueprint names. A version that cannot import them refuses a file that holds some. At import, a zone
  identical to one of mine is reused, and another under the same id takes a free id, the blueprint rewritten to name it.
- **Import never writes over a file**: the blueprint keeps its id when no blueprint and no file of the folder holds
  it, in any case, else takes a free one. One of D3B's is exported as mine based on it, in the player's words.
- **The system's dialogs** come from `tauri-plugin-dialog`, called from Rust only: the webview gets no permission;
  a file dropped on the window is previewed by its path.

## Rejected

- A zip of `blueprint.json`, `zones/` and `routes/`: unreadable at a glance, a crate more, and more ways to fail
  (a corrupt archive, paths that escape).

## Consequences

A blueprint's new `schema` needs no new `d3b`: its migration reads it in a `.d3b` too. A change to the envelope's shape
raises `d3b`, with a test that reads the former one. A `.d3b` can be read, diffed and pasted as text.
