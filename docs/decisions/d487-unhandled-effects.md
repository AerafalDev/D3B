# Fights, the effects the engine left out (D487)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The formulas are
[D195](d176-d208-step-9.md); a push's damage [D214](d209-d252-step-9.md) and [D289](d253-d292-step-9.md); the bench by
breed [D463](d463-fights-by-breed.md); the blows a push stops [D485](d485-ally-strikes.md); open question Q5 in
[the open questions](open-questions.md).

## D487: The damage received reads every resistance, reduction and share, and a cast moves its caster

*2026-11-21, step 9, the owner's ask for an audit of the effects and states the engine does not handle; the first batch
chosen by the owner ("1+2+3 puis 4") among the audit's ranked list. Refines D195, D214. Builds D3, D463.*

- **Measured first.** `scripts/fights.cs --details` now counts the effects the engine leaves out, cast and given as buffs,
  by id (`effects left out ...`). On the 217 game captures of 18 to 21 November (client 3.7.14.18), of the 107,808 blows
  compared, 4,938 were dealt above the range foreseen and 2,749 below. 4,064 of those above struck a target whose
  resistance to every element (characteristic 101, effects 1076 and 1077) was lowered, which the formula never read;
  2,440 of those below struck a monster holding a buff of 105, a fixed reduction of the damage received. The ranked audit
  of the rest is kept in `artifacts/studies/unhandled-effects.md` (Git-ignored).
- **Made.** The resistance to every element adds to the element's own (`FightCharacteristic.ResistPercent`), as the
  references count it (Bubble's `GetElementMainResist`); their cap of 50 for a player is not applied, no captured character
  reaching it. A buff of 105 names its points by its parameter; they are raised by a twentieth of the fighter's level
  (`EffectFormulas.Reduction`, the references' reductor) and taken off each blow after the fixed resistance and before the
  resistance in percent: measured flat, 1,500 blows stayed below by 1 to 5; raised, 229 of the 2,749 are left.
- **Imagined.** A cast giving a buff on the damage received that waits for any damage (trigger `D`) changes every blow
  after it in the AI's fight: a share of 1163 (a Féca's Bouclier Féca, an Iop's Bond and Massacre, a Sram's, a Crâ's, an
  Ouginak's, the Forgelance's Holmgang: 2,096 casts of the characters) and a reduction of 105. One waiting for some damage
  only, such as a distance's (`DR`), is left out. A share only maybe given is left out rather than halved.
- **The caster moved.** 1042 moves the caster towards the fighter it reaches, stopping beside it, and 1041 away from it,
  as that fighter would pull or push it (Bubble's `PushUtils`): 3,156 casts of 15 breeds, the owner's Zobal, Forgelance,
  Eliotrope and Pandawa among them. Measured: 1,892 of 1,892 moves towards and 1,072 of 1,160 away land on the cell
  imagined, the 88 others nearly all an Eliotrope's Odyssée cast through portals (left to the portals). A caster moved away
  and stopped short took the push damage of its own level and bonus, not its target's: 433 casts, the formula with the
  caster's matching each one checked.
- **The bench's own counts.** The server names a 1020 shield as 1040 and a 1079 or 1080 removal as 168 or 169, given as a
  buff: matched by its source effect, the buffs whose target was not foreseen fall from 43,429 to 4,914, and the AP removed
  within the range from 602 to 2,940.
- **Replayed** with `--turns` on the 217 captures, main ac1c7fe6 against the branch: the turns played foresee 312,229 damage
  for 316,624 announced (309,924 before) and 2,053 kills for 2,059 (2,077 before); the plans casting rise from 7,671 to
  7,864, those that only walk fall from 688 to 512. By breed, the damage planned rises for the Pandawa (50,309 to 52,906),
  the Forgelance (31,646 to 32,880), the Sacrieur, the Iop, the Enutrof and the Zobal, holds for the Féca and the Crâ, and
  falls by 0.2 % for the Eliotrope and 0.8 % for the Huppermage; the kills planned fall by 3 to 8 for the Enutrof, the Iop,
  the Sacrieur and the Zobal, foreseen nearer what the server announces.
- **Rejected.** Every effect of the audit at once: the dodge of AP and MP, the damage reflected (107), erosion and the
  Sacrieur's states are each a session's work, ranked in the audit. A share of 1163 ended with its buff's rounds: the search
  covers the turn and the reply, inside which the captured shares last.
