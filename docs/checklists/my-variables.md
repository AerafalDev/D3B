# Step 3, my variables

Status: open

**step 3, my variables** ((14)'s first PR, the owner's choices of 2026-11-17), on Windows x64 from `bun run app` on
`next` or the next release:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 75c2e7b: item 5 passes, and 2, 3 and 6 but
for what was not tried (the file, the help line, the bounds narrowed to 0 to 4000, Escape, a click only choosing,
Récolte automatique's variables). Not measured yet: items 1 and 4.

1. open Récolte automatique: no "+" on Variables, its variables do not drag, Details only says them;
2. in a copy of mine, "+" on Variables: "Nouvelle variable" joins the list, chosen, Details showing its fields;
   name it "Objectif", type Nombre, default 5000, bounds 0 to 10000, "Réglage par personnage" on, an help line:
   each is in `Documents/D3B/blueprints/<id>.json` at once (`"id": "nouvelle-variable"`), and Ctrl+Z (outside a
   field) undoes them one by one;
3. give the copy to a character: its tab shows "Objectif" at 5000; set it to 7000 there, then in the editor narrow
   the bounds to 0 to 6000: the character's 7000 goes (its file holds no `objectif`) and its tab says 5000; narrow
   them to 0 to 4000: the default becomes 4000;
4. Space on the graph: the picker offers "Objectif" and "Écrire Objectif"; add both, wire "Objectif" into
   "Attendre … min", type 3 on "Écrire Objectif", then make Objectif a Texte: the wire goes, the 3 with it;
5. "Supprimer la variable": it goes with its Lire and Écrire nodes and their wires, without a question; Ctrl+Z
   brings all back.
6. drag "Objectif" from My Blueprint onto the graph: its name follows the pointer, and a menu "Lire Objectif" /
   "Écrire Objectif" opens where it is dropped; Escape closes it; drag again with Ctrl held: a Lire node at once,
   where it was dropped; with Alt: an Écrire node; a click on "Objectif" still only chooses it; in Récolte
   automatique, its variables do not drag.
