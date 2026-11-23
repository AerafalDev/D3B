# Step 3, a character's blueprint and settings

Status: open

**step 3, a character's blueprint and settings** ((10)'s Zone field and (11)), on Windows x64 from `bun run app` on
`next` or the next release, with format 4's data:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1, 3, 4, 6 and 8 pass, and 2,
5 and 7 but for what was not tried (the arrow opening the blueprint's tab, "Zone de A et B", the file's schema and an
older file still reading). Not measured yet: item 9.

1. play a character: its tab says "Blueprint", "Aucun blueprint" in the list, its arrow greyed, no setting; then
   its state and map as before;
2. choose Récolte automatique: Zone "À choisir", Banque à 90 %, Banque "La plus proche" and Garder "Rien", both
   tagged "étape 5"; the arrow opens the blueprint's tab; the help of each setting shows on hover;
3. Banque à: type 5, leave the field: it turns red, then 90 again; type 80, Enter: a blue bar on its left and a
   reset button "Revenir à la valeur du blueprint (90 %)"; click it: 90, the bar gone; type 90 again: no bar;
4. click Zone: the list unfolds under it, "Mes zones" first, then the sub-areas by area and level; type "foret",
   arrows and Enter choose Forêt d'Astrub: the field says "Forêt d'Astrub · Astrub · niv. 20", marked; its target
   opens the World tab with a banner for the character, a hovered map outlines its whole sub-area, one click
   chooses it and the character's tab comes back with it; Escape on the map cancels and comes back too;
5. in the World tab, choose Forêt d'Astrub in the view: Details says "Zone de <the character>"; a second character
   given the same Zone: "Zone de A et B"; another sub-area: no "Zone de";
6. set Banque à to 80, then choose Récolte d'un métier: the Zone and Banque à 80 are kept, marked; Métier "À choisir" lists the harvesting
   professions with their icons, by name; choose Bûcheron: marked; choose "Aucun blueprint", then Récolte
   automatique again: back to its defaults;
7. a second character keeps its own values; restart D3B: both kept, in `Documents/D3B/characters/<server>-<id>.json`
   (`"blueprint"` and `"settings"`, `"schema": 3` since (23)); a file written before keeps reading;
8. close a character's client: its offline tab shows the same block, and a setting changed there is kept when it
   plays again;
9. duplicate Récolte automatique, give it to a character, then delete its file by hand and restart D3B: the list says "Blueprint
   <id> introuvable" and no setting shows.
