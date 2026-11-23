# Step 3, Advanced tools

Status: open

**step 3, Advanced tools** ((24)), on Windows x64 from `bun run app` on `next` or the next release:

Measured by the owner on 2026-11-17, Windows x64, `bun run app` on `next` 60cf228: items 1, 2, 3 and 5 pass, and 4 but
for the palette's command, not tried. The owner then asked for the panel closed by default and opened by a character
online (2026-11-17), measured below. Not measured yet:

4. "Afficher ou masquer les outils avancés" in the palette: Advanced tools turn off and the panel closes; restart:
   they stay off;
6. write `{"schema": 9}` in `settings.json` and restart: a notification says the file is kept as it is and why,
   and Advanced tools are off; turn them on: a notification says the setting holds until D3B closes and why, and
   the file is unchanged.
