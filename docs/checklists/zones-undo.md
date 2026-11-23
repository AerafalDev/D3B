# Step 3, undo and redo of my zones

Status: open

**step 3, undo and redo of my zones** (the owner's request of 2026-11-17), on Windows x64 from `bun run app` on
`next` or the next release, with format 4's data:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 75c2e7b: items 1 and 2 pass, and 4 and 6 but
for what was not tried (the file and the trash, the stroke undone at once). Not measured yet: items 3 and 5.

1. "+" makes "Nouvelle zone"; paint two strokes with the brush, then a whole sub-area with S: Ctrl+Z three times
   undoes the sub-area, then each stroke, one stroke a step; Ctrl+Y twice and Ctrl+Shift+Z once bring them back;
2. rename it in Details, then Ctrl+Z on the map: the old name is back in the row, the top left and Details; while
   typing in the name's field, Ctrl+Z undoes the typing only;
3. click a row of "Mes zones" in the World view, then Ctrl+Z there: the change is undone the same way;
4. delete the zone from its row: Ctrl+Z brings it back, open on the map with its maps, its file in
   `Documents/D3B/zones` again (its copy stays in the system's trash); Ctrl+Y deletes it again;
5. make a second zone on Incarnam and paint a stroke, then open the first zone on the Twelve: Ctrl+Z undoes the
   stroke, the map going to Incarnam with the second zone open; Ctrl+Z again removes it, sent to the trash, and
   the map closes it;
6. a stroke then Ctrl+Z at once, quickly: the stroke is undone, never the change before it; restart D3B: Ctrl+Z
   undoes nothing (the history lasts the session).
