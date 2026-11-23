# 0021. The game layer reads everything early and acts through typed acts with deadlines

- Status: accepted
- Date: 2026-11-23
- Decided by: the owner

## Context

Step 4 makes a character play in game ([play.md](../play.md)). Every action of steps 4 to 7 and every blueprint node
that reads the game stands on the game layer, so its shape is costly to change. The owner asked for it "much better and
much more complete than the C#" and able to back "as many things as the Lua scripts". On `main`, each feature watched
the frames in its own observer with its own copy of the state, its delays scattered per feature, and five waits without
end reached players in three days (D464, D481, D484, D488, D490). `next` pinned 18 messages on 2026-11-23; the C#
version's `.proto` files declare about 262.

## Decision

- **One owner of the state**: `game::Character` holds the whole state of the character and its map, fed by the
  protocol; each field names its source message and whether it is verified, inferred or not decoded yet.
- **Read everything early, act step by step**: the first PRs of step 4 decode, read only, every family the C# version
  reads, each checked on the owner's captures replayed; an act that sends comes with the step that uses it. A generated
  coverage report lists, per build, each message pinned, decoded, encoded and measured, and the Lua call it backs.
- **Typed acts**: each act is a state machine started on a view and polled on a given clock, ending `Done`, `Refused`
  with its typed reason, `NoAnswer` past its deadline, or `Interrupted`; one table holds every deadline with its
  measure.
- **The C# version's measures are the first source** of each act, as named tests; every request is proven byte for
  byte against the C# bot's own in the owner's captures.
- The bankers come from the extractor's NPC tables, a format 6, not from values written in the code.
- **The bot's frames go between the client's**: the relay holds the client's bytes until its frame in progress ends,
  then writes the bot's, with the request id the C# version measured for them (D61); the server's answers reach the
  client. This amends ADR 0011, whose relay wrote nothing of its own: it still rewrites only its one frame.
- **Pins follow the families read**: this amends ADR 0012's pins "for the messages a feature reads": a family the C#
  version reads is pinned and decoded once the owner's captures check it, before a feature uses it.

## Rejected

- Decoding only what each step uses: faster to the first harvest, but the state stays as partial as the C#'s for weeks.
- Reading and sending everything now: acts coded without a use, unmeasured in game, as `main`'s fifteen features tested
  only on a fake game.
- Async functions per act with timeouts: a suspended future cannot be kept, shown or resumed (ADR 0017's reason).
- The three bankers measured written in the code: few banks, and client values in the repository.

## Consequences

Step 4 starts with the writer (two PRs) and four PRs of reading before the paths and the first walk; each adds its
families to the coverage report and its fields to [game.md](../game.md). A blueprint node is a thin call onto a field or an act. Review refuses a wait
without a deadline and an act whose refusals are not typed.
