# 0019. The blueprints' catalogue is written whole and coded in slices with the actions that use it

- Status: accepted
- Date: 2026-11-23
- Decided by: the owner

## Context

v2 (ADR 0016) planned its nodes as PRs (v2.5) to (v2.9), queries, events, actions and the Expression, all before any
action plays in game. The owner then asked for blueprints "complete": actions on events, the character's states, and
more. An audit of 2026-11-23 compared `next` with `main`'s C# bot, the protocol and the reference Lua API, and found
gaps: no choice type, no way to read a monster group's fields, missing events, settings `main` had. `main` built a
blueprint studio (D293 to D318) that nothing used, and fell back to forms.

## Decision

- **The whole catalogue is written now** in `docs/bot.md` ("The catalogue, complete"): every event, query, action,
  flow node and type, each with its slice and the data it needs, the never-list's rows marked never.
- **A node is coded with the first action or D3B blueprint that uses it**, not before. The language is frozen at
  (v2.4); (v2.5) to (v2.9) are re-cut into slices of steps 4 (Play, Travel), 5 (Récolte in game), 6 (Combat) and 7,
  and "on demand" for nodes no blueprint uses yet.
- **Records** (a monster group, a resource here, a sub-area, a fighter) with a generic Break node, the queries keeping
  the common filters as inputs; records live on wires, locals and a run's variables, never in settings or memory.
- **A choice type** whose named options live on its variable, a Switch taking them as cases.
- Banking stays per character, D3B's blueprints sharing the bank settings' ids; a whisper is a notification, never an
  event; an invitation is refused when unhandled, its event coming with step 7's groups; long actions stay atomic,
  events being their hooks.

## Rejected

- Coding (v2.5) to (v2.9) first: kinds nobody runs, as `main`'s studio; their design drifts before their user exists.
- Queries with filters only, no records: each new filter would wait for a version of D3B.
- Banking general to the app (`main`'s D405): a setting outside any blueprint, a concept the model does not have.
- Events for other players' presence or words: a graph could wire a flight, which `CLAUDE.md` refuses.

## Consequences

Each slice's PRs bring an action with the nodes it needs; a node over the game coded before its slice is a review
finding, and a kind not in the catalogue is added there first, with the owner. A choice's options on a variable and
the new types change the file's shape: the first slice bringing one brings schema 3 and its migration from 2.
