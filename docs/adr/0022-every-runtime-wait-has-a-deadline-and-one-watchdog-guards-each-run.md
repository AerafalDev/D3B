# 0022. Every runtime wait has a deadline and a typed outcome, and one watchdog guards each run

- Status: accepted
- Date: 2026-11-23
- Decided by: the owner

## Context

From step 4 the runtime waits on the game: a walk's arrival, a map change, a use's end, a dialog or the bank opening,
the server's answer. The C# version hung on such waits and was then kicked by the server's idle timeout, about 30
minutes (its D464, D481, D484, D488 and D490): the player found a character logged off with no reason given. ADR
0021 makes each act of the game layer typed, its deadlines in one table; this rule holds for every wait of the run.

## Decision

- **Every wait the runtime makes on the game has a deadline** and ends in a typed outcome: done, refused (the
  server's reason), too long (what it waited for, how long), or cut (disconnected, a fight). Each deadline is named
  in code beside the gesture it bounds, from a measure; an action turns the outcomes into Done or Failed with their
  reason, so Retry and an unlinked Failed see them.
- **One watchdog per run**: while running, if D3B sends nothing to the game for **10 minutes**, the run turns
  blocked, "nothing sent for 10 min", with the node, the inner step and what it waited for. The graph's own waits
  count too (a Wait of 40 minutes blocks, on purpose), so no run ever reaches the server's kick; a Wait of 10 minutes
  or more, and a Wait until whose longest wait is 10 minutes or more or none, say so beside their node in the editor.
  The night pause logs off (ADR 0019).
- Paused, finished, blocked or away, a run has no watchdog. Before step 6 a run entering a fight sends nothing,
  notifies, lets the player fight by hand and resumes its action after: that fight is not counted.

## Rejected

- 5 minutes with the graph's waits left out (the owner's first answer that day): a Wait of 25 minutes or more would
  still meet the kick; one rule for every wait is simpler to explain.
- No watchdog, deadlines only: a bug between two waits, or a flow waiting on a state that never comes, would hang.

## Consequences

Each action of steps 4 to 7 lists its waits and their deadlines and is tested on each outcome, too long included, on
the injected clock. Every asynchronous test, in Rust (`tokio::time::timeout`) and in TypeScript, has a deadline of its
own too, so a hang fails that test, never CI's job (`.claude/rules/tests.md`). Review checks that no wait on the game
or in a test is unbounded.
