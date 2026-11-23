# Step 3, a given blueprint's update

Status: open

**step 3, a given blueprint's update** ((23)), on Windows x64 from `bun run app` on `next` or the next release:

1. give a character Récolte automatique and set Banque à to 80; quit D3B; in its file
   `Documents/D3B/characters/<server>-<id>.json`, set `"blueprintVersion": 0` and add `"gone": 1` to `"settings"`;
   start D3B: its tab says above its settings "Récolte automatique v1 : ta valeur de gone ne s'applique plus.",
   Banque à still 80, and the file says `"schema": 3`, `"blueprintVersion": 1`, `"dropped": ["gone"]`, no `gone`;
2. close a client: its offline tab shows the same notice; click its ×: the notice goes, and stays gone after a
   restart (`dropped` gone from the file); restart again: nothing changes;
3. choose Chasse, then Récolte automatique: no notice; the file's `blueprintVersion` follows (1), and none for a
   copy of mine;
4. when a release first ships a given blueprint at version 2: a copy made before shows a blue arrow on its
   Library row, its hover and its tab's line saying "son original a une version plus récente, v2"; a character
   running it keeps its values, a notice naming any lost.
