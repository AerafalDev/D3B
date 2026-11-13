# 0013. The bot runs Unreal-like blueprints, set per character

- Status: accepted
- Date: 2026-11-13
- Decided by: the owner

## Context

The C# version's profiles, routes and zones were too many concepts, tedious to create, rigid and opaque
(`docs/lessons.md`). Step 3 designs what the player tells the bot from scratch. On 2026-11-12 and 2026-11-13 the owner
went through mock-ups (prioritised activities, rule rows, lanes by priority) and a prototype in the real shell, and
chose "very complete but very simple, like Unreal".

## Decision

- **A blueprint is a class, a character an instance**: a character runs one blueprint; a variable marked "set per
  character" (Unreal's Instance Editable) is a setting whose default the blueprint holds and whose value each
  character sets.
- **One kind of blueprint**: D3B's ship locked; Duplicate makes one of mine, which notes what it is based on. A given
  blueprint's new version reaches the characters that run it, their values kept.
- **An event graph as Unreal's**: free nodes and wires; events with a priority and one of four modes (interrupt then
  resume by default, wait for the action, restart, in parallel for what never acts); thresholds re-arm; functions may
  hold actions that last; a goal is a variable.
- **Goal-shaped actions**: an intent and a done-check on the observed state, ending Done or Failed with a reason; an
  interrupted action runs again; an unlinked Failed blocks the character with its reason; events the game forces have
  defaults.
- **Rust owns the catalogue, the validation and the runtime** (`src-tauri/src/bot/`); the editor is a typed view.

## Rejected

- Prioritised activities with flows inside: two levels, a concept more, and the owner wanted a free graph.
- Lanes by priority on one sheet: the owner found the canvas confusing next to Unreal's.
- Settings on the blueprint, with copies per zone: two kinds of copies for two zones.
- A goal field on the character: a variable tested in the graph does it.

## Consequences

The player learns one model, Unreal's. Every action of steps 4 to 6 is written goal-shaped with its done-check, and
the editor refuses a wrong link by type. Review checks that no second kind of blueprint or of setting appears.
