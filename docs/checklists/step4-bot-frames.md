# Step 4, the bot's frames in the relay

Status: open

**step 4's F2, the relay writing the bot's frames between the client's** ([relay.md](../relay.md#the-bots-frames)), on
Windows x64 with client 3.7.14.18, from `bun run app` on `next` once its PR merged. Nothing sends yet: this measures
that the game plays as before. Never paste a capture. Turn on Advanced tools first (Affichage > Outils avancés).

1. Start D3B, then a client; enter the game and open the character's tab: play five minutes (walk, change maps,
   harvest, open the bank, a fight if one is at hand): everything answers as before, and the panel's Protocol header
   keeps "non fiables 0" and "perdus 0";
2. with `D3B_RECORD` set to an absolute folder, play a minute: the capture is written as before,
   `<build>-<ms>-<n>.d3bcap`.
