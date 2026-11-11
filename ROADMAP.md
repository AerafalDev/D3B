# Roadmap

Each step is designed with the owner before code (`feature`), delivered in small PRs, and **measured in game by the
owner** before the next one starts. The current step is in [docs/now.md](docs/now.md).

0. **Foundations**: the Tauri shell, typed IPC, packed data, i18n, the Claude setup, CI and releases for six targets.
1. **Game data**: the .NET extractor, cleaned up (`tools/extractor`), writing one JSON folder per client build; its
   `client-<build>` release; the app reads it. The world first (maps, world graph, positions, sub-areas, areas, zaaps,
   the world map and its tiles, texts in five languages); items, monsters and spells come with the step that reads
   them (ADR 0008).
2. **Observe** (milestone 1): the relay and the client's redirection, the protocol and its per-build mapping, and a
   live view of each character: where it is, its map, life, pods, what is around it, and a protocol inspector.
   Nothing is sent to the game yet.
3. **The bot's model**, designed from scratch, not ported: what the player tells the bot and how they see what it
   does. Starting points to study: default profiles with options, a node graph (blueprints) for anything custom,
   goals the bot plans for itself. It must fix what went wrong before: too many concepts, tedious to create, rigid,
   opaque ([docs/lessons.md](docs/lessons.md)). An interactive mockup is decided before any code.
4. **Act**: moving on a map and between maps, travel with zaaps and NPC ways, "the player wins" (a manual action pauses
   the bot), recovery after an interruption.
5. **Harvest and bank**, on the model of step 3.
6. **Fights**: the fight state, the rules of a cast and a walk, the effects, the AI; then the hunting activity.
7. Then, each in its own design: several accounts and groups, the marketplace and crafting, treasure hunts and
   dungeons, the profession scripts of `vendor/metiers` as the acceptance test.

**Shared knowledge**, designed with the owner once step 2 reads the protocol: what only the server tells, learned
from every player's sessions into a hosted database (free, its host to choose), and read back by every D3B. Nothing
about players is ever sent: no character or account name.

- **Fight placements**: the server draws a fight's map, often not the one left (107 of 163 fights), and its cells,
  which the client's data does not flag. One row per map left, map fought on and set of cells: the map left with its
  area, sub-area and coordinates, the map fought on, its red (challengers') and blue (defenders') cells, how often and
  when seen (the C# version's `Placements`, `main:docs/decisions/d253-d292-step-9.md`, D275).
- **NPCs on maps**: the map, the NPC, its actor id, cell and direction, how often and when seen, then their dialogues
  for quests (`LearnedMapNpc`, `main:docs/decisions/d369-d389-step-7.md`, D383).
- **Bug reports**, and what else the owner adds.

When step 2 is measured on Windows, macOS and Linux, `next` replaces `main` and releases stop being pre-releases.
