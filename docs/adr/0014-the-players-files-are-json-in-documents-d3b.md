# 0014. The player's files are one JSON per object in Documents/D3B

- Status: accepted
- Date: 2026-11-13
- Decided by: the owner

## Context

Step 3's blueprints, zones, routes and characters' values are the player's own work: they must survive updates, be
backed up and shared, and be read by later versions. The C# version's formats changed often.

## Decision

- **Where**: `Documents/D3B`, visible, easy to back up or put in git.
- **One JSON file per object**: `blueprints/`, `zones/`, `routes/`, and `characters/` holding a character's blueprint,
  its own values and its memory variables. Each has a stable id and a `schema` version; a blueprint's layout (where
  its nodes sit) is kept apart from its logic, so a moved node never changes what it does.
- **Written atomically** (a temporary file, then a rename), saved as one types. A file of a newer schema, or one that
  does not read, is kept untouched and reported, never overwritten.
- **History** as `history/<character>/<day>.jsonl`, kept 7 days.
- **Sharing** as one `.d3b` file bundling a blueprint with the zones, routes and functions it uses, imported after a
  preview. D3B's own blueprints ship inside the app, read-only.

## Rejected

- The app's data directory with a "reveal" command: hidden from the player who owns the files.
- One database file: neither diffable nor shareable piece by piece.
- History for the session only, or 30 days: lost on closing, or too large to read.

## Consequences

Every change to a file's shape bumps its `schema`, with a migration and a test that reads the former shape. Files
edited by hand are read like any other, and refused with their reason when they do not hold.
