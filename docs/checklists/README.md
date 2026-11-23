# In-game checklists

One file per feature: what the owner measures in game on Windows x64, and the testers on the other platforms
([testing.md](../testing.md)), before the feature is done (`CLAUDE.md`, Done). `docs/now.md` lists each file still
open or waiting in one line, which `bun run rules` checks.

A file's status is its line `Status: <status>` under its title, which `bun run rules` reads:

- `open`: an item, or the part of one not tried, is still the owner's to measure. While more than five are open, no
  new feature PR is opened (`CLAUDE.md`, The cap); CI fails a `feat` PR then, unless it carries the `step-finish`
  label: one of the remaining PRs of the feature already started, which `docs/now.md` names.
- `waiting`: what is left waits on an outside event (a release, a client patch, a tester, an account), each item
  saying which; it does not count toward the cap.
- `measured`: every item passed; the file stays as the record of its measures.

A measure adds its line, "Measured by the owner on <date>, <platform>, <how>: items … pass", which is never rewritten
afterwards; "but for what was not tried" keeps the untried part open. A checklist is written with the feature's last
PR, from its design's measure.

## The game data

From the monsters' PR on, `next` reads format 5 only, published on 2026-11-17 (from the PR's b9412ad, the owner's go).
A checklist that says "with format 4's data" runs on `next` before that PR or from the last release, or on a later
`next` with format 5's: `bun run data:fetch <build>`, the client's build (3.7.14.18 since 2026-11-16).
