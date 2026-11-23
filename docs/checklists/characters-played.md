# Step 3, the characters played

Status: open

**step 3, the characters played** (PRs #226 and its view), on Windows x64 from `bun run app` on `next` or the next
release; on macOS and Linux, Forget's trash by testers:

1. play a character, close its client: its row stays under a line, greyed, "Vu à l'instant", then "Vu il y a 1 min"
   a minute later; its file is in `Documents/D3B/characters/<server>-<id>.json`;
2. restart D3B: the row is still there; click it: its tab says "Hors ligne" and "Vu pour la dernière fois aujourd'hui
   à <heure>, à <sous-zone> (x, y)", without a map or gauges;
3. with that tab open, start the character's client again: its row goes back above the line, and the tab becomes
   its live tab (map, gauges);
4. play a character, change map, wait two minutes, quit D3B while playing: its tab, once D3B is back, says the
   time and place of within a minute before quitting;
5. right-click an offline row, Forget: the row goes, the file is in the system's Recycle Bin and can be restored;
   Ctrl+Shift+P, "Oublier <nom>" does the same;
6. make a character's file unreadable by hand (a stray letter): the row is gone and D3B's log names the file kept.
