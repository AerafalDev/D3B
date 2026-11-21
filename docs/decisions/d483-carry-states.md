# Fights, a carry and the states that forbid it (D483)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The carrying is
[D474](d474-pandawa-carrying.md); the audit that led here is [D480](d480-cooldowns-merged.md).

## D483: A carry takes no fighter whose state keeps it from being moved or carried, nor one Enraciné or under Pesanteur

*2026-11-21, step 10, the owner's hint while the session looked at Karcham's refusals ("faut pas qu'elle ait l'état
enraciné et autre, ou état lourd"), the owner's ask to read Bubble for what looks wrong, and the owner's answer that
neither Enraciné nor Pesanteur is carried. Refines D474.*

- **Read in Bubble**, the reference read for ideas (D3): the server refuses no cast for its target's states; a carry
  takes nothing when the target holds a state whose state effects hold 3 or 4, nor a monster that may not be carried.
- **Read in the pack of 3.7.14.18**: state effect 3 is held by exactly the 22 states flagged as unable to be moved,
  Indéplaçable (97, 379) and Emprisonné (252) among them; 4 by 5 others, Lourd (63) and Porteur (3) among them.
  Enraciné (6), not pushed nor swapped, and Pesanteur (7), not swapped, hold neither.
- **The owner's answer**: neither a fighter Enraciné nor one under Pesanteur is carried in the game, whatever the pack says.
- **Measured** on the captures of 2026-11-20 and 21 (`scripts/fights.cs --live --details`, a throwaway reading): none of
  the 17 targets of the refused Karcham, nor of the hundreds cast, held any of those states, the same states on both.
  The 17 refusals stay unexplained: not the target's states as the bot reads them.
- **Made**: `CastRules.StatesLetCarry`, read by `Imagination` before a carry: no fighter is carried while one of its states
  holds the state effect 3 or 4, or is Enraciné or Pesanteur, so that the AI no longer imagines carrying, then throwing,
  such a fighter. Test: `CarryTacticsTests.AFighterIsCarriedUnlessOneOfItsStatesKeepsItFromBeingMovedOrCarried`, a fighter
  only kept from being pushed still carried. No capture holds such a carry; not replayed with `--turns`, the captures holding
  no target the rule changes. Left: state 3366, also named Pesanteur, with no effect and no flag, not read.
- **Rejected**: Bubble's reading alone, which carries a fighter Enraciné or under Pesanteur, against the owner's word;
  refusing Karcham's cast on such a target in the rules, which Bubble does not and no capture measures; the flag "cannot be
  moved" alone, which leaves Lourd out.
