# Step 1, the extractor

Status: measured

**step 1, the extractor**, on the installed client (Windows). Measured by the owner on 2026-11-11, Windows x64:
items 1, 2 (every count as below, in 27.8 s) and 4 pass; on 2026-11-13, `bun run app` on `next` 3454d38, item 5
passes (the new status bar's "Aucune donnée de jeu" and the format in its hover); on 2026-11-13, `next` 506b1e7,
item 3 passes (a second run writes the same `manifest.json`, every count as in item 2): step 1 is measured on
Windows x64.

1. `dotnet run --project tools/extractor/src -- --help` lists `--client` and `--output`;
2. `dotnet run --project tools/extractor/src` prints build 3.7.13.17 and 15,470 map positions, 569 sub-areas, 70
   areas, 63 zaaps, 40 worlds, 161 zooms, 1,250 tiles, 17,623 maps, 46,490 elements, 11,331 vertices, 30,917
   edges, then exits 0 in about 30 s, writing `artifacts/data/3.7.13.17/` and `artifacts/data/client-3.7.13.17.tar.gz`;
3. a second run writes the same `manifest.json`;
4. `tar -xzf artifacts/data/client-3.7.13.17.tar.gz -C data`, then `bun run app`: the status bar's right item says
   "Client 3.7.13.17", and its hover names no error;
5. with `"format": 2` in `data/manifest.json`, the status bar says there is no data and its hover names the format.
