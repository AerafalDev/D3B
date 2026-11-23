# Step 3, the .d3b file

Status: open

**step 3, the `.d3b` file** ((22)), on Windows x64 from `bun run app` on `next` or the next release:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1, 2 and 3 pass, and a
`.json` refused (item 5). Not measured yet: item 4, the rest of item 5, and item 6.

1. right-click Récolte automatique in the Library, Exporter…: the system's save dialog offers
   `Récolte automatique.d3b`; save it: a notification names the file; it is indented JSON beginning `"d3b": 1`;
2. on another D3B (or after moving the file away from `Documents/D3B`), Importer… in the Library's header, choose
   the file: the preview shows its name, description, "Basé sur Récolte automatique v1", Zone, Pods and Banque under
   "Réglages par personnage" with their defaults, no problem, and what it holds; Annuler: nothing in "Les miens";
3. drop the file on the window, Importer: it joins "Les miens" as `recolte-automatique` and its tab opens, its graph
   the same as the given one's; import it again: `recolte-automatique-2`, the first untouched;
4. export one of mine with the Exporter button of its tab, import it: the same nodes, wires, values and layout;
5. drop a `.json`, a `.d3b` cut in half, and one whose `"d3b"` is 2: each is refused with why (not a .d3b, its line
   and column, a newer D3B), and nothing is added;
6. paint a zone `Blé` of three maps, set it as the Zone default of a blueprint of mine, export it, delete the zone,
   import the file: `Blé` comes back with its three maps and the blueprint names it; import it again: the same zone
   is reused, no `ble-2`; paint a fourth map on `Blé`, import once more: a zone `ble-2` holds the three maps and
   the new blueprint names it, `Blé` keeps its four.
