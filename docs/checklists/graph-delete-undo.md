# Step 3, deleting and undoing in a blueprint's graph

Status: open

**step 3, deleting and undoing in a blueprint's graph** ((13)), on Windows x64 from `bun run app` on `next` or the
next release:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: item 6 passes, and 2 to 5 but for
what was not tried (Ctrl+Shift+Z, Delete with the Library's list focused, a right-click inside a box, two nodes moved
together undone in one step, the file following each change, after a restart). Not measured yet: item 1, and Ctrl+Z in
the picker's search (item 4).

1. open Récolte automatique: a click on a node then Delete or Backspace removes nothing, a right-click on a node
   offers no "Supprimer", Ctrl+Z changes nothing;
2. duplicate it and open the copy: click "Récolter", press Delete: it goes with its four wires, at once, without
   a question; Ctrl+Z (the key Z of the keyboard's layout, AZERTY's too): it comes back where it was, with its
   wires; Ctrl+Y: it goes again; Ctrl+Shift+Z after a Ctrl+Z does the same; choose a node, click the Library's
   list, press Delete: nothing goes;
3. Shift and drag a box around two nodes (or Ctrl and click the second), then Backspace: both go with their wires;
   one Ctrl+Z brings both back; right-click inside the box: "Supprimer" deletes both; move two nodes dragged
   together, then Ctrl+Z: both go back in one step;
4. right-click a node: "Supprimer · Suppr" deletes it; right-click a wire: "Supprimer" deletes that wire alone;
   right-click the empty graph: the picker opens, no menu; type in the picker's search and press Ctrl+Z: the text
   is undone, not the graph;
5. make three changes, close the tab and reopen it: Ctrl+Z still undoes them, one at a time, and the file in
   `Documents/D3B/blueprints/<id>.json` follows each; restart D3B: Ctrl+Z undoes nothing;
6. give the copy to a character, then right-click it in the Library: "Supprimer" (absent on D3B's) sends it to
   the system's trash without asking and closes its tab; the character's tab says "Blueprint <id> introuvable";
   restore the file from the trash and reopen the Library: it is back, and the character runs it again with its
   values.
