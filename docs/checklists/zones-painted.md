# Step 3, my zones painted

Status: open

**step 3, my zones painted** ((20)), on Windows x64 from `bun run app` on `next` or the next release, with format 4's
data:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: item 1 passes, and 2 to 5 but for
what was not tried (Space with the main button, V then a click, F2, Renommer, a blank name or Escape, the file's
form); a zone of mine is listed first in a character's Zone field. Not measured yet: choosing it there (item 6), its
deletion (7) and a file kept (8).

1. the World view says "Mes zones" above the sub-areas, "Aucune zone : « + » en peint une."; click "+": "Nouvelle
   zone" appears with 0 carte, the World tab opens on the world it showed, the brush pressed in the toolbar on top,
   "Nouvelle zone" and "Zone à moi · 0 carte" on top left, Details with its name and "Aucune : peins ses cartes.";
2. press and drag over Forêt d'Astrub: each map crossed turns amber as the pointer goes, the sea stays as it was;
   release: the row says N cartes; the right button and Space with the main one move the map without painting;
3. E, then drag over painted maps: they go; S: hovering outlines a whole sub-area; click Champs d'Astrub: all its
   maps turn amber; click it again: they go; V, then click a sub-area: it is chosen, the zone and toolbar go;
4. type a name in Details' field, Enter: the row and the top left follow; a blank name or Escape puts it back;
   F2 on the row and Renommer in its right-click do the same in place; Details lists Forêt d'Astrub with its share
   ("12/68") and the Bûcheron's woods;
5. restart D3B: the zone is there with its maps, in `Documents/D3B/zones/nouvelle-zone.json`, one map a line;
6. in a character's tab, Zone lists it first under "Mes zones" with its maps; choose it: "<name> · Zone à moi · N
   cartes"; the World tab's Details of the zone says "Zone de <the character>";
7. right-click the row, Supprimer: it goes to the system's trash, the map closes it, the character's Zone says
   "Zone nouvelle-zone introuvable"; put it back from the trash and restart D3B: everything is back;
8. put a stray letter in a zone's file: the Library's "Ignorés" lists it, and it is never written over.
