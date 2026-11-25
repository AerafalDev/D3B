# Dungeons, the first version's shape and the rooms (D520 to D525)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The activities' model
is [D87](d064-d090-step-4.md); the NPCs' dialogs, [D253](d253-d292-step-9.md) and [D385](d369-d389-step-7.md); the groups,
[D429](d429-d435-step-10-groups.md) and [D476](d476-groups-game-following.md); the world map's hints, [D414](d414-step-7-hints-on-the-world-map.md);
a run restarted, [D519](d518-d519-never-idle.md).

## D520: A dungeon is an activity of its own that reads the client's dungeon table and follows the server from room to room

*2026-11-25, roadmap step 12, the owner's ask ("faudrait mettre la possibilité de faire des donjons aussi"), the shape the
owner's choice between the two the session put. Builds D87, D253, D385, D414, D519.*

- **Measured**, the client 3.7.14.18 (`scripts/inspect.cs -- dump`, its `World.DungeonsDataRoot`): 187 dungeons, each with its
  rooms (`mapIds`, 1 to 15, 763 maps in all), its entrance and exit maps (set on all, equal on 152), its minimum level, its
  bosses (141 monsters, every one a boss) and the objects it requires (126 dungeons need one key of type 84, quantity 1;
  60 of the 61 with none are the "Expédition" copies). The sub-areas' entrances and exits are empty on all 569: this table is
  the only place the data says where a dungeon's door is. The keyring is item 10207.
- **Measured**, the owner's 74 game captures (`scripts/capture.cs`, types, ids and times only): 12 connections went into four
  dungeons, Champs, Château Ensablé, Bworks and Forgerons, 32 character runs and 148 fights, all played by hand. The keeper on
  the entrance map is talked to (action 3), a key or keyring reply and a confirmation answered, and the first room's map comes
  28 to 41 ms after, a key spent (20 of 20) and the keyring nothing. At a room's win the server names the next room in the
  fight end's millisecond, unasked, 128 times; some rooms are left on foot through map actions or a door the world graph
  holds. After the boss the server moves the character to a map of the dungeon's sub-area that is no room, where an NPC's exit
  reply sends it to the exit map (28 exits). The rooms' order in the table is not the walk's: Champs was entered at its fourth.
  No message is a dungeon's own; D3B maps every one these use.
- **Made**, the design: the pack gets the dungeon table, and an activity `dungeon` reads where its character is and does what
  that map wants, as D87 asks: away, it checks the level and the key and travels to the entrance; on the entrance, it talks to
  the keeper; on a room, it fights the room's group and lets the server move it; on the dungeon's sub-area off the rooms, it
  talks to the NPC there and leaves; out, it banks when full and enters again until the profile's count of runs is done.
  Built in three pull requests, test-first on the fake game: entering, the rooms' chain, the end.
- **Rejected.**
  - A route step in the Sequencer, the rooms written map by map: a route never banks, fails at its first failed action and
    needs the rooms' order, which only the server knows. The activity can be a behavior's node as every activity is.
  - Rooms walked in the table's order: the captures contradict it.

## D521: The dungeon is made for a character alone first, the group following once it works

*2026-11-25, roadmap step 12, the owner's choice between the two the session put. Builds D429, D476, D490, D520.*

- **Measured**: in every captured dungeon run the characters were a party; each talked to the keeper itself with its own key,
  the leader attacked each room's group and the others joined as anywhere. One reply "Téléporter les membres du groupe."
  was answered once, its effect not measured. Today a follower reaches its master's map through an NPC only by a measured
  way (`NpcWays.Measured`, two), so a master entering a dungeon leaves its followers behind (D490).
- **Made**: the three pull requests of D520 serve a character alone. A fourth teaches the follower to enter behind its master:
  when the master's map is a dungeon's room, it travels to that dungeon's entrance and talks to its keeper with its own key,
  then joins the room's fight as now.
- **Rejected.**
  - The group from the first pull request: closer to how the owner plays, but it would make every step wait on the groups'
    first measure in game (10b, 10c), and each pull request larger.

## D522: Every keyed dungeon of the table, the keepers' and the exits' replies chosen by their French text

*2026-11-25, roadmap step 12, the owner's choice between the two the session put. Builds D253, D385, D520.*

- **Measured**, the client's NPC tables: each reply id has a text of its own, so no text id is shared from keeper to keeper.
  "Donner la clef et entrer." is 91 reply ids on 65 NPCs, "Utiliser le trousseau de clefs." 117 on 96, the confirmation "Oui."
  265, "Sortir." 40 and "Sortir du donjon." 9. The keeper whose "Reprendre …" reply names its dungeon is found for 118 of the
  126 keyed dungeons. No table places an NPC on a map: the server shows them on arrival. The final map is marked nowhere.
  The four measured keepers' ids (Champs 780: 2802 or 6615, then 20904; Ensablé 798: 2944, 20326; Bworks 775: 2794, 20302;
  Forgerons 1162: 6633, 20336) agree with their texts.
- **Made**: the keeper is the NPC of the entrance map the server shows that offers a key or keyring reply; each reply is
  chosen among those the question offers by its text in French, which the pack always holds (D385) whatever the client's
  language; the exit NPC is the one of the final map offering an exit reply. Every keyed dungeon is offered; the four measured
  ones are the tests' facts, the others to be seen in game.
- **Rejected.**
  - The four measured dungeons only, their reply ids written down as `NpcWays` are: sure, but each new dungeon would need a
    capture and a change of code.
  - The replies by their text id: none is shared.

## D523: A dungeon is entered with the keyring when the keeper offers it, else with a key carried, else the activity ends

*2026-11-25, roadmap step 12, the owner's choice between the two the session put. Builds D520, D522.*

- **Measured**: a key is spent at each entry (`ObjectDeletedEvent`, 20 of 20 entries with a key); the keyring spends none.
  The keyring holds one effect 814 per keyring dungeon (106), but D3B keeps no item's effects, so whether it may be used
  is known only when the keeper offers its reply. 128 of the 129 keys' recipes are Bricoleur's; no reference buys or crafts keys.
- **Made**: at the keeper, the keyring's reply when offered, else the key's when the character carries the dungeon's key;
  with neither, before the journey when no key and no keyring is carried, the activity ends saying why, an end no restart
  changes (D519).
- **Rejected.**
  - A key taken from the bank first: D3B cannot take an item out of a bank yet; it comes next. Buying and crafting keys wait
    for step 11.

## D524: A profile lists several dungeons, entered in turn, those the character cannot enter left out

*2026-11-25, roadmap step 12, the owner's ask ("faudrait faire en sorte qu'on puisse farm plusieurs donjons différents
aussi"), the order and what is left out the session's within the ask. Refines D520 and D523.*

- **Made**: the activity's setting is a list of dungeons instead of one. Each entry goes to the next of the list in turn after
  the dungeon last entered, the first time the list's first; a dungeon whose level the character lacks, whose key and keyring it
  does not carry, which the client's table lacks, or whose keeper turned it away in the run is left out of its turn. With one
  dungeon listed the activity ends saying what keeps it shut; with several, once none can be entered, saying so. A keeper
  turning a dungeon away, its keyring's charge spent for instance, shuts that dungeon for the run alone.
- **Rejected.**
  - Each dungeon run a number of times before the next: a count per dungeon would be a setting more, while keys running out
    already move the turn on; the runs' count, if the owner wants one, is the end's (the third pull request).
  - One dungeon per profile, a list of profiles for several: a sequence of profiles would end each on its first dungeon
    shut, where the turn leaves it out and goes on.

## D525: In a room the character regenerates, fights its groups and lets the server move it; a won room it was not moved from is left on foot, and a fight lost leaves the dungeon for the run

*2026-11-25, roadmap step 12, the rooms' chain, the second pull request of D520, the owner's go ("Oui, les salles"); the
choices the session's within D520. Builds D87, D153, D236, D384, D520, D524.*

- **Measured** (client-game.md, "The game, in a dungeon"): at a room's win the server names the next room in the fight end's
  millisecond, unasked, 128 times; Forgerons' and the Bworks' rooms are also left on foot, through map actions and a door the
  world graph holds; the table's order of the rooms is not the walk's. Nothing measured a lost fight inside.
- **Made**: in a room of a listed dungeon, its information come, the step waits up to 5 s for a group; with one, the character
  regenerates to the profile's share of life (`LifeShare`, 80 % by default, a point each interval the server gave, D153), then
  fights the room's groups as a route's fight does (`MapFight`, D384), the AI playing each; the next step is the next room's.
  A room with no group is won: the character travels through the graph to the nearest room of the dungeon not yet won in that
  run, stopping on the first that shows a group; with every room won, or on a map of the dungeon's sub-area that is no room,
  entrance or exit, the map the boss's win leads to, the run of the dungeon ends, the exit being the third pull request's.
  A fight lost leaves that dungeon for the activity's run, a restart included, the others of the profile still entered.
- **Rejected.**
  - The rooms walked in the table's order, or the next room guessed from it: the captures contradict the order.
  - A fight lost answered as the hunt's (resume, pause or stop, D198): a dungeon entered again after a loss spends another key
    on a fight the character just lost; the other dungeons of the profile go on instead.
  - Food before a room's fight, as the hunt's: kept for the third pull request's profile, with the bank.
