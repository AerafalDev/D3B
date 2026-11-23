# Step 3, a blueprint's graph edited

Status: measured

**step 3, a blueprint's graph edited** ((12)), on Windows x64 from `bun run app` on `next` or the next release:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1 to 5 pass: (12) is measured
on Windows x64.

1. open Récolte automatique: its nodes do not move, no wire can be drawn, a right-click or Space opens nothing;
   the wire from "Déposer à la banque" back to "Aller dans" runs under the nodes, no line leaving the screen, and
   no mini-map in the bottom right corner;
2. duplicate it and open the copy: drag "Récolter"'s Échec onto the empty graph: the picker opens there, "après
   « Échec »", "Souvent ici" Prévenir, Attendre, Terminer, then the rest by family, only what takes the flow;
   "Aller dans …" tagged "étape 4"; type "prev": Prévenir alone; Enter: a Prévenir node where the wire was
   dropped, wired from Échec;
3. drag the Zone pill onto "Pods": the pin refuses it and says "Cette entrée ne prend pas ce que la sortie porte."
   beside the pointer; release: nothing changes; drag it onto "Zone": the old wire is replaced;
4. drag "Au lancement"'s output onto Prévenir: its wire to Récolter goes; a right-click, then a click on the graph
   and Space with the pointer on it, open the picker with every node, "Lire" and "Mettre" the variables and the functions;
5. move a node, close the tab and reopen it, then restart D3B: the nodes and wires are where they were left,
   in `Documents/D3B/blueprints/<id>.json`; the Library's count of problems follows the graph's (a Prévenir with
   no text: one).
