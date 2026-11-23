# Step 3, renaming a blueprint of mine

Status: open

**step 3, renaming a blueprint of mine** (the owner's request of 2026-11-17), on Windows x64 from `bun run app` on
`next` or the next release:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 75c2e7b: item 4 passes, and 1 to 3 but for
what was not tried (the description not a button, no "Renommer" in the Library's right-click on it, the file; in 3,
only one of "Renommer" and F2 may have been tried).

1. open Récolte automatique: its name and description are not buttons; right-click it in the Library: no
   "Renommer", and F2 on its row does nothing;
2. open a copy of mine, click its name: a field; type "Mes blés", Enter: the tab's title, the Library's row and
   `Documents/D3B/blueprints/<id>.json`'s `"name"` follow, the file keeping its name and `"id"`; click the
   description, empty it, leave: "Ajouter une description" shows, and the file holds no `"description"`;
   Ctrl+Z (outside a field) puts the description back, then the name;
3. in the Library, right-click it, "Renommer": its row becomes a field, its name chosen; type "Mes bouftous",
   Enter: renamed; F2 on its row, type, Escape: unchanged; F2, empty it, Enter: unchanged;
4. a character running it keeps running it, its tab naming it "Mes bouftous"; export it: the `.d3b` is named
   "Mes bouftous.d3b".
