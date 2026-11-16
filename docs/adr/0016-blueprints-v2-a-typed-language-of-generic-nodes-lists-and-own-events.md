# 0016. Blueprints v2 are a typed language of compact values, generic nodes, lists and events of the player's own

- Status: accepted
- Date: 2026-11-16
- Decided by: the owner

## Context

v1's catalogue (25 kinds) covered D3B's three blueprints and the six scenarios. Measuring the editable graph, the owner
found it far too poor ("much more complete, so that we can do much more"): no loops, no many-way branch, one
comparison, one sum, no lists, no timer, no event but the six fixed ones. ADR 0014's model (an Unreal-like event
graph, one flow per character, goal-shaped actions) holds; what it lacks is a language.

## Decision

- **Values are compact operator nodes** as in Unreal (`≥`, AND, `+`…), without flow, wired by type; no formula node.
- **Generic pins and lists**: a wildcard pin takes the type of its first wire, every wildcard of a node the same;
  every type has its list. Map, item, duration and time of day join the types; Wait's minutes become a duration.
- **Flow** as a bot needs it: Sequence, Switch, Repeat, While, For each, Leave the loop, Do once, Do N, Alternate,
  Wait until (with a longest wait), Trigger and timers, Block, Pause; not Unreal's whole list.
- **Events of any condition and of the player's own**: "When … becomes true", re-armed when false or by its own
  "Re-arm when"; Every, At; events declared by the blueprint, fired by Trigger or a timer, each with a priority and a
  mode as the game's.
- **Functions** gain named exits, locals and the pure function; **no macros**, since functions already hold actions
  that last.
- **Randomness** is in the catalogue (a number, a chance, a list's element): the player's to use. Nothing about
  other players is.
- **A loop that never lasts** is caught at run time (10,000 nodes in a row without a lasting one block the character),
  not refused in the editor.
- **What D3B does not observe yet** (the inventory's content, professions' levels, quests) is listed, marked, and
  studied at the step that reads it.

## Rejected

- A formula node, alone or beside the nodes: a second language with its syntax and typos.
- One node per type (`=` of numbers, `=` of zones…): the picker floods, as Unity Visual Scripting's.
- Only fixed game events: every new threshold would wait for a version of D3B.
- Macros: they overlap functions almost entirely here.
- A problem in the editor for a cycle without a lasting node: it would refuse loops that end by their condition.

## Consequences

The blueprint file goes to schema 2, with a migration from 1 and a test reading 1 (ADR 0015). The catalogue, the
links' check (Rust and the editor's drag), the Summary and the runtime ((25), (26)) all know wildcards and lists.
Review checks that a value node never has a flow pin, that no kind duplicates a generic one per type, and that a
query reads only what `game::Live` observes or is marked with its study and step.
