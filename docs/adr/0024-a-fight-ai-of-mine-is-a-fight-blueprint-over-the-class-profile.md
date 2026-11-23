# 0024. A fight AI of mine is a fight blueprint of its own, over the class's profile

- Status: accepted
- Date: 2026-11-23
- Decided by: the owner

## Context

The owner asked for fights a player can script: a custom AI in blueprints, mount and archmonster captures. The
fight's direction (2026-11-16) made a fight profile, an ordered list of rules, the class's default AI, with "On my
turn" for advanced players; it did not say where a player's turn logic lives, how it shares the turn with the
profile, or how captures are offered. The C# version had one AI (D244), and none of capture is measured.

## Decision

- **The class's fight profile plays by default.** "On my turn" overrides it, and two nodes hand the turn back:
  "Let the built-in AI play this turn" and "Let it play one action", the profile's next rule that holds.
- **A fight blueprint** holds a player's turn logic: a blueprint of ADR 0014's one kind whose graph holds the
  fight's events (placement, on my turn, a turn began, a fighter died, a fight ended), chosen in a character's setting
  (none by default) and for one fight by the character's blueprint, through Fight's input; shared alone, per class
  or per fight. The character's run waits through the fight while it plays, one flow at a time (ADR 0017).
- **Captures** are settings of Combat and Chasseur that the built-in AI plays (which wild mounts, which soul stone
  for which archmonsters; keep the target alive, cast at the right moment), plus a Capture node and the fighter's
  fields for a fight blueprint; each studied and captured at step 6 before it is coded.

## Rejected

- The AI only as a blueprint per class, no profile: a simple turn long to read, and every class one graph to keep.
- The profile only, no graph: what the rules cannot say, a player cannot do.
- Turn logic in the character's blueprint: each blueprint copying its fight logic, a Switch on the class in each.
- Captures as nodes only: D3B's blueprints could not capture, the common case left to advanced players.

## Consequences

No second kind of blueprint (ADR 0014 holds): the fight's events run only while their blueprint plays a fight, and a
fight node outside their flows is a problem. Step 6 designs the profile and the fight blueprints together; review
checks a fight node runs only in a fight.
