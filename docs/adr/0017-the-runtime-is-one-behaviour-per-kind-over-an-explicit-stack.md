# 0017. The runtime is one behaviour per kind over an explicit stack, advanced on a given clock

- Status: accepted
- Date: 2026-11-16
- Decided by: the owner

## Context

Step 3's (25) runs blueprints (ADR 0014). v2's catalogue (ADR 0016) brings about 130 kinds, loops, Sequence, Switch,
functions with exits, locals and pure ones, its kinds landing in many PRs after the engine. Every action of steps 4 to
7 is written against the runtime, so its shape is costly to change. The C# version kept "one actor per character over
immutable state snapshots, injected time" (`docs/lessons.md`).

## Decision

- **One `Behaviour` per kind**, in a registry by kind id; the engine knows none of them, Get, Set and Call apart, so a
  new kind plugs in without touching it.
- **An explicit stack of frames**, a call's and a chain's (a loop's body, Sequence's outputs), not async: a waiting run
  is data, resumed after an interruption with its loops and calls.
- **Values pulled** when the node that uses them runs, each traced for the debugger.
- **A synchronous `Run::advance(view, now)`**, its clock injected: tests drive it on a fixed clock and on the owner's
  captures; step 4's Play drives it from one actor per character fed `game::View` snapshots. (25) delivers the engine
  alone.
- **A blueprint compiled** into a program before it runs, so the file's schema stays out of the engine.
- An action before its step waits for its check on the observed state; memory lives in the character's file.

## Rejected

- Wiring the run in the app now, with a dry-run line in the tab: two PRs and a mock-up for what step 4 shows anyway.
- Running it inside the connection's observer, on its tick: it would die with the connection and move at step 4.
- Async functions per kind: a suspended future cannot be kept, shown by the debugger or resumed elsewhere.
- Waiting for v2's model before the engine: the compiled program already keeps the schema out (the owner chose speed).
- A stubbed action failing, or done, at once: its check would go untested until step 4.

## Consequences

Each v2 PR brings its kinds' behaviours with their tests. Step 4 writes an actor and the doing of each action, not a
second engine. Review checks that no kind is matched inside the engine.
