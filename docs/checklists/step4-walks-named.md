# Step 4, the walks named

Status: open

**step 4's F3, the movement family pinned and the bot's requests encoded** ([protocol.md](../protocol.md#requests)), on
Windows x64 with client 3.7.14.18, from `bun run app` on `next` once its PR merged. Nothing sends yet. Turn on Advanced
tools first (Affichage > Outils avancés), and open the panel's Protocol view under the character's tab (Ctrl+J).

1. Walk on the map, then to the next map, a few times: the list names the client's `MapMovementRequest`,
   `MapMovementConfirmRequest` and `MapChangeRequest`, and the server's `MapMovementEvent` and
   `MapMovementConfirmResponse`; click a `MapMovementRequest`: its detail reads `key_movements` and `map_id`;
2. through all of it, the header keeps "non fiables 0".
