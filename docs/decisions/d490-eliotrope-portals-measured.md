# Fights, an Eliotrope's portals measured (D490)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The portals in the
AI are [D468](d468-eliotrope-portals.md#d468-an-eliotropes-portals-in-the-ai-cast-through-by-every-character-of-the-bot),
the marks on the cells [D280](d253-d292-step-9.md#d280-glyphs-walls-and-the-marks-as-a-turn-begins-and-ends).

## D490: An Eliotrope's portals measured, a walk or a push that ends on one sent through it

*2026-11-21, the owner's ask ("voir comment fonctionnent les portails et les sorts à travers dans Bubble, voir si tout est
ok car j'en avais pas l'impression"), the lead's task; settled by the session, measured rules. Refines D468.*

- **Measured** with `scripts/fights.cs` on the 217 game connections of 3.7.14.18 of 18 to 21 November, each group fight
  recorded by its 8 accounts (counts below are of the connections): 2,178 casts of Portail (14574, its 1181 of dice 2 and
  44338, value 0), 32 of 14582 (1183), no other portal set. 1,329 casts named 2 to 4 portals they went through: 937 an
  Eliotrope's, the others a Pandawa's, a Zobal's, a Forgelance's, a Sacrieur's, an Iop's and an Enutrof's, none an enemy's.
- **The cell.** D468's chain foresaw the cell and the exit the server named 1,313 times; the 16 others were two ties of
  distance, which the server broke as the references do, by the angle around the portal (D3, Bubble's `PortalUtils`): of
  portals as near, the one turning clockwise of the other, or of two opposite the first from the map's bottom. Made so,
  1,329 of 1,329.
- **The share.** For each damage dealt through portals and not capped by the target's life, the shares that make it
  foreseen: on the 665 of casts that cast another spell (the Eliotrope's), 2% a cell, the client's text and D468's count,
  fits 473, 2 more as Bubble counts it (the effect's dice once more) 400, none 313; on the 312 of the others, 184, 200
  and 120. D468's count is kept; Bubble's is not taken.
- **What a cast through portals casts.** Each of the Eliotrope's cast spell 14631 on it, effects 1171 and 2971 of 2 for 3
  turns, 2% more damage and heals dealt; the server gives it in the characteristics, which the bench and the AI read. Not
  imagined.
- **The walk and the push through a portal.** Of the walks and slides that ended on an open portal, by both teams, 777 of
  818 were teleported onto the cell of the portal a cast on it comes out of; none of 152 walks that passed over a portal on
  their way; none of 64 that ended on a portal a fighter went through that turn. The server says it with a 307 striking the
  mover, then an action 1181 whose payload D3B does not read, then the teleport.
- **Made.** `Portals.Moved` sends a fighter that ends a walk or a push on an open portal, a character or a monster the pack
  lets use portals, onto the chain's last, its own cell left counting for none, and closes the two (`CellMark.IsUsed`), no
  cast nor move going through a closed one; `Portals.Reopened` opens them as any turn begins, as the references do.
  `Imagination.Walked` and `Imagination.Cast` move so, the search's walks before a cast cast from the portal they come out
  of, and so do the reply's. The fight's state closes two portals when the server teleports a fighter from one onto another of
  its team, and opens them as a turn begins (`GroundMark.IsUsed`), so that the AI, which plans again after each answer, sees
  them closed. `scripts/fights.cs` compares each move onto a portal and each cast through portals with the engine: 1,297
  of 1,330 moves end where the engine foresees them, the 33 others monsters of the other team the server did not send
  through, and 1,329 casts of 1,329 land where it foresees them.
- **Replayed** with `scripts/fights.cs --turns` on the same 217 captures, 8,771 turns, main ac1c7fe6 then this change, run
  beside four other sessions' benches: the Eliotrope's 1,089 turns plan 48,616 damage instead of 48,472, 488 kills
  instead of 477, 114,240 over two turns instead of 111,524, 55 plans doing nothing instead of 64 and 56 walks alone
  instead of 72; the life its reply takes 10,029 instead of 9,286, and 9,067 instead of 8,600 on its turns played, the
  enemies' walks onto its portals now imagined. The Pandawa's damage 0.8% up, its plans doing nothing 64 to 60; the
  Zobal's 0.8%, its kills 243 to 247; the Forgelance's 0.4%, 361 kills to 365; the Enutrof, the Iop and the Sacrieur
  within 0.2%; the Féca, the Cra and the Huppermage line for line. All breeds: 318,635 damage planned for 317,577, 2,430
  kills for 2,410, plans doing nothing 392 to 379. The searches 163 ms then 161 halfway, 337 then 318 at 90%.
- **Not measured in game**: the AI walking onto a portal on purpose. **Left**: the closing effect 1183, which the AI
  never casts; 14631 imagined; the action 1181's payload, which would give the server's own word on a portal open or
  closed; the 33 monsters' moves onto an open portal the server did not send through.
- **Rejected.**
  - Bubble's share, the dice once more: it fits fewer of the damage dealt.
  - A portal's cell out of the walks: the walk through a portal is a way across the map the server gives, which the
    search weighs as any other.
  - Reading the action 1181's payload first: a mapping format and a release for what the teleport already tells.
