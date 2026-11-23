# Step 3, values typed on a node's inputs

Status: open

**step 3, values typed on a node's inputs** (the owner's request of 2026-11-17), on Windows x64 from `bun run app`
on `next` or the next release:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 75c2e7b: item 1 passes, and 2 and 3 but for
what was not tried (the Library's count of problems, the file, "Zone" and "Pods" saying "relié"). Not measured yet:
item 4.

1. open Récolte automatique and choose "Récolter": Details says its inputs' values, no field;
2. in a copy of mine, add a Prévenir and choose it: Details shows "Texte" as a field; type "Objectif atteint",
   press Enter: the node's sentence says it, the Library's count of problems drops by one, and the file
   `Documents/D3B/blueprints/<id>.json` holds `"inputs": {"text": "Objectif atteint"}`; Ctrl+Z (outside the field)
   takes it back, Ctrl+Y puts it again;
3. choose "Récolter": "Zone" and "Pods" say "relié", without a field; add "Attendre … de vie" from the picker and
   choose it: set "Vie" to 101: the field is marked and put back when left; set 80: a button "Revenir à la valeur
   par défaut (90 %)" shows; click it: 90 %, and the file holds no `life` on that node;
4. add "Écrire Banque à" (Banque à is bounded from 10 to 100): 5 is refused, 50 kept; add "Aller dans …": its
   "Zone" is the Zone field, its target choosing on the World's map and coming back to the blueprint's tab; clear
   a Prévenir's text and press Enter: its value goes and its problem comes back.
