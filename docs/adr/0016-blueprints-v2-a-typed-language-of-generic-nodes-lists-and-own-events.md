# 0016. Blueprints v2 are a typed language of compact values, generic nodes, lists and events of the player's own

- Status: accepted
- Date: 2026-11-16
- Decided by: the owner

## Context

v1's 25 kinds held D3B's three blueprints. Trying the editable graph, the owner wanted blueprints "much more complete"
and "as practical as a Lua script" (the reference bots' scripts): no loops, one comparison, no lists, no timers, no
event but six. ADR 0014's model (an event graph, one flow per character, goal-shaped actions) holds.

## Decision

- **Values**: compact operator nodes as Unreal's, and a read-only **Expression** node, a typed formula checked by Rust
  with completion, for Lua's one line. **Randomness** (a number, a chance, a list's element) is the player's to use.
- **Types**: wildcard pins take their first wire's type; every type has its list; map, item, NPC, duration, time of
  day and **ranges** ("from level N: a value", edited as a table) join them; Wait's minutes become a duration.
- **Flow** as a bot needs it (Sequence, Switch, loops, Do once, Alternate, Wait until, Retry, Pick from a list,
  Trigger and timers, Block, Pause), not Unreal's whole list.
- **Events** of any condition ("When … becomes true", re-armed when false or by its "Re-arm when") and of the
  player's own, fired by Trigger or a timer, with a priority and a mode as the game's.
- **Functions** gain named exits, locals and the pure function; no macros, no library shared between blueprints.
- **Routes**: an action and a forced exit per anchor, resumed from the current map, recorded while playing, as text.
- A loop that never waits is caught at run time (10,000 nodes); unobserved data is marked and studied at its step.

## Rejected

- Nodes only, or a formula only: five boxes for one line, or a second language for everything.
- One node per type: the picker floods, as Unity Visual Scripting's.
- Only fixed game events: every new threshold would wait for a version of D3B.
- Macros and a shared library: they overlap functions and per-character settings here.
- An editor problem for a cycle that never waits: it would refuse loops that end by their condition.

## Consequences

The file goes to schema 2, migrated from 1 (ADR 0015). The check of links, the Summary and the runtime know wildcards,
lists and expressions. `CLAUDE.md`'s "Never" still holds: D3B's own blueprints and docs never use randomness to pass
for a player. Review checks that a value node has no flow pin, that no kind repeats a generic one per type, and that a
query reads what `game::Live` observes or names its study and step.
