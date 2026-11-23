# Step 2, the protocol

Status: open

**step 2, the protocol** ([protocol.md](../protocol.md)), on Windows x64 with client 3.7.13.17 or 3.7.14.18, from `bun run app` on
`next` or the next release. Never paste a capture. Measured by the owner on 2026-11-12, Windows x64, `bun run app`
on `next` b200248: items 1 to 6 pass; its two captures replayed through the relay byte for byte and through the
protocol with no frame not trusted; on 2026-11-13, `bun run app` on `next` d978253, items 1 and 3's counts in the
inspector and its items 8 and 9 pass. Not measured yet: item 7, to take now with v0.14.0 on client 3.7.14.18 (out on
2026-11-16), which v0.14.0 has no pins for, before the next release, which will (`next` has them since 2026-11-17).
Since the new shell (the owner, 2026-11-13), the relay's hover no longer lists the connections: items 1, 2, 4, 5
and 7 read the Characters view and a character's tab; the counts of items 1 and 3 read the inspector: the panel's
"Protocol" view under the character's tab (Ctrl+J shows the panel), its counts on its header's line. Since step 3's
PR 24, items 1, 3, 7, 8 and 9 begin by turning on Advanced tools: Affichage > Outils avancés (or "Afficher ou
masquer les outils avancés" in the palette), which the next starts remember.

1. With the data of the client's build in `data/` (the extractor's item 4, or `bun run data:fetch <build>`), start D3B, then a client; enter the game and open the
   character from the Characters view: its tab names the coordinates the game shows for its map, and the panel's
   Protocol header says "non fiables 0" and "perdus 0";
2. walk to the next map: within about a second the tab names the new coordinates;
3. play five minutes (walk, open the inventory, harvest, a fight if one is at hand): "compris" and "lus" grow,
   "non fiables" and "perdus" stay at 0;
4. play a second account at once: two connections, each with its own map;
5. close one client's window: its character leaves the Characters view, the other stays;
6. with `D3B_RECORD` set to an absolute folder, play a minute: the capture is named `<build>-<ms>-<n>.d3bcap`, its client's build;
7. after the next client patch, before its pins: the Characters view says "Protocole non lu", the character's tab
   that its build has no pins, the Protocol panel "Aucune trame gardée." without counts, and the game plays as before;
8. the inspector, while playing: its frames scroll in, newest last; scroll up: the list stands still while frames
   come; click a `MapCurrentEvent` row: the detail on its right reads `map_id` and no message name; click a dimmer
   row (a message not pinned): its fields by number, a length for anything else; type "Map" in the filter: only
   names holding it; choose "↑ Client": only the client's; press the pause: the list stops and says how many
   arrived, press it again: they are listed; switch to a second character's tab: its own frames and counts;
9. click a "secret" row: the detail says its content is never kept.
