# Fights, a carry and the states that forbid it (D482)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The carrying is
[D474](d474-pandawa-carrying.md); the audit that led here is [D480](d480-cooldowns-merged.md).

## D482: A carry takes no fighter whose state keeps it from being moved or carried

*2026-11-21, step 10, the owner's hint while the session looked at Karcham's refusals ("faut pas qu'elle ait l'état
enraciné et autre, ou état lourd") and the owner's ask to read Bubble for what looks wrong. Refines D474.*

- **Read in Bubble**, the reference read for ideas (D3): the server refuses no cast for its target's states; a carry
  takes nothing when the target holds a state whose state effects hold 3 or 4, nor a monster that may not be carried.
- **Read in the pack of 3.7.14.18**: state effect 3 is held by exactly the 22 states flagged as unable to be moved; 4 by 5
  others, Porteur (3) among them; state 6, which no push nor swap moves, holds neither, nor does Pesanteur (7).
- **Measured** on the captures of 2026-11-20 and 21 (`scripts/fights.cs --live --details`, a throwaway reading): none of
  the 17 targets of the refused Karcham, nor of the hundreds cast, held a state with either effect, the same states on
  both. The 17 refusals stay unexplained: not the target's states as the bot reads them.
- **Made**: `CastRules.StatesLetCarry`, read by `Imagination` before a carry, so that the AI no longer imagines carrying,
  then throwing, such a fighter. Test: `CarryTacticsTests.AFighterIsCarriedUnlessOneOfItsStatesKeepsItFromBeingMovedOrCarried`.
  No capture holds such a carry; not replayed with `--turns`, the captures holding no target the rule changes.
- **Rejected**: refusing Karcham's cast on such a target in the rules, which Bubble does not and no capture measures; the
  flag "cannot be moved" alone, which leaves out the 5 states of effect 4.
