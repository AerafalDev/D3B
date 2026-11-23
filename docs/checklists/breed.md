# Step 2, the breed in a character's tab

Status: open

**the breed in a character's tab** (the owner, 2026-11-13: its name in the player's language and its head, 40 px in
a rounded square in place of the initials, chosen from mock-ups; then the same in the Characters list), a stack
against `next`: (1) the data's format 2, `breeds.json` and the 38 heads, named in a second asset of the build's
release (done in code); (2) the character's sex, read from its look (done in code: the pins' audit holds, and the owner's 54 game connections
replay with no frame not trusted); (3) the tab's breed name and head (done in code); (4) the
Characters list's head (done in code). Measured by the owner on 2026-11-13, v0.14.0 installed: items 3 and 4
pass. Not measured yet: items 1 and 2 (the owner's own extraction and `data:fetch`), 5 now with v0.14.0 on client
3.7.14.18, whose data it lacks.

On Windows x64:

1. on `next` at 25c5c4c or before (format 2; since the items' PR the extractor writes format 3),
   `dotnet run --project tools/extractor/src` also prints "Breeds: 19 breeds, 38 heads of 64 px" and writes
   `artifacts/data/client-3.7.13.17-format2.tar.gz`; then `tar -xzf artifacts/data/client-3.7.13.17-format2.tar.gz
   -C data` (after emptying `data/` but its README): a `data/` still in format 1 makes `bun run app` say there is
   no game data and fails the Rust test that reads `data/`. On a later `next`, the same with format 4's archive,
   `client-3.7.13.17-format4.tar.gz`, whose run also prints the breeds' and the hints' lines;
2. on the same commit, `bun run data:fetch 3.7.13.17` fills `data/` with format 2 (published on 2026-11-13, from
   `next` 2b0577c), and `bun run app`'s status bar says "Client 3.7.13.17"; on `next` at 55e4be8 or later, the
   same with format 4 (published on 2026-11-16);
3. with that data, start D3B, then a client, and open a character from the Characters view: its tab shows its
   breed's head, 40 px in a rounded square, in place of its initials, the head of its sex, and "Niveau <n> ·
   <breed> · <state>" with the breed's French name (in English after switching the language: "Level <n> ·
   <breed>", Crâ becoming Cra); a character of each sex if one is at hand;
4. the Characters view shows the same head, 28 px, in place of the initials, on each character's row;
5. at the next client patch, before its data: the tab and the character's row show the initials, and the tab
   "Niveau <n>" alone.
