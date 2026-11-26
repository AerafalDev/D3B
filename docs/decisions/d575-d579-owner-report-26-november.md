# The owner's report of 26 November: the transfer's own load, a run begun on a walk, lists in the bank, full pods, mules, the harvest's shut maps (D575 to D579, D581, D582)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the transfer's lasting
refusals and a run begun in a storage left open ([D572 and D573](d572-d573-transfer-refusals-and-open-storage.md)) and the app's
1.1.2 ([D574](d574-release-1-1-2.md)).

The owner's report of 2026-11-26 evening, on the app's 1.1.2: the Pandawa still standing still and attacked, the Huppermage's and the
Sacrieur's « Bank Into Guild » ending before all was in the chest, the bank's moves a stack at a time, the Sacrieur stuck after a
profile change, an option to go on full without the bank, an option for mules in fights, and every inconsistency of the captures. The
lead session measured the owner's 103 game connections of 20 to 26 November and the bots' journals from 25 November 21:07 UTC, by
four studies in the session's `artifacts/studies/` (git-ignored): `panda-still.md`, `transfer-stops.md`, `fights-2026-11-26.md` and
`travel-profiles-2026-11-26.md`. On 1.1.2 the Pandawa stood still for no way no more (D571 held: 258 journeys, none without a way) and
all its 35 fights were a resource's protectors; its losses of 25 and 26 November were to level 150 to 158 groups, its profile then
without the safe level, which the owner's save of 14:31 UTC put back.

## D575: « Empty the bags into the bank first » works from the bank to the guild's chest too, the bags' objects going to the chest with the bank's

*2026-11-26, the lead session, the owner's choice (« Oui, l'inventaire aussi ») of the lead's recommendation. Supersedes D564's
rejection of the option from the bank.*

- **Measured** on the Huppermage's run of 16:50 UTC and the Sacrieur's of 16:56 (`…OBE1`, `…OBE0`): neither stopped early. The
  Huppermage's took and put 140 stacks in 16 rounds, none refused, and left the bank empty; the Sacrieur's left one bound object
  the chest refuses (text 356, D572). What stayed was each character's own harvest, 5,970 of 10,136 pods and about 5,500 of 6,321,
  which the owner put in the chest by hand after the run, as on the three mules at 12:52. The owner's profile had the option on,
  which D564 kept to the other way and hid from this one.
- **Made**: the option is shown and played both ways. From the bank, the run's first step opens it, puts in by one request every
  stack the bags hold but what is worn, counts nothing of it as moved, and goes on with the round's takes in the same opening, a
  bank left open to the next step being the busy wait's (D484). The bags' stacks the bank took are no longer noted as the
  character's own, an object keeping its id out of the bank and back.
- **Rejected.**
  - Keeping D564's line, the guild given none of the player's own objects: the owner's mules carry their harvest for the guild.
  - Leaving the bank after the deposit and opening it again: a banker's dialog more, about two seconds a run.

## D576: A run begun on a walk or a use a stopped run left going settles it before its first step, as it leaves a storage

*2026-11-26, the lead session. Refines D573.*

- **Measured** on the Sacrieur's connection of 14:29 (`…OBE0`): at 16:56:53.6 its harvest sent a walk the server showed and the
  client never confirmed; the owner's profile change stopped the harvest 0.7 s later, before its walk's delay settled it. The new
  run's first step found the character walking, its busy wait waiting on it; D3B sent nothing for 23 s, until the owner stopped,
  started again and walked the character by hand. The only such case in the 103 connections.
- **Made**: `ActivityRun` puts the character in order through `Recovery.SettleAsync` before a run's first step when it is walking,
  using an object or held by the server, as already when it is in a dialog.
- **Rejected**: a shorter busy wait, which would still wait on a walk no step of the run sent.

## D577: The bank's moves go by lists: what goes in by one request now, what comes out by the client's list once mapped

*2026-11-26, the lead session, the owner's choice (« Poser et retirer en liste ») over the lead's of the puts alone first.*

- **Measured** on the owner's 103 connections ([client-game.md](../client-game.md#the-game-transferred)): in the bank the client puts
  in a list by `ExchangeTransferFromInventoryRequest`, which D3B's deposits already send (D80), and takes out a list by a packed list
  of ids (70 times) or everything (27 times), requests the readable protocol lacks, with the events that answer them; in the guild's
  chest it moves only a stack at a time (239 moves). The server refuses a bank action asked less than about 0.55 s after the last
  (text 268).
- **Made** for the puts: the transfer puts in the bank by one request every stack it carries whole, then a stack at a time a stack
  its take joined to the character's own, whose number it gives. What the bank leaves, which it answers with nothing, goes a stack
  at a time after.
- **To make** in a session of its own: the client's list take and its answers in the readable protocol, a mapping of format 27
  published first (D24), the state that follows them, and the transfer's takes from the bank by it, cut short by the pods as the
  client's are.
- **Rejected**: the takes by list in this session: a protocol change and a mapping to publish, a task of its own (D17).

## D578: A profile may go on full rather than unload, the fights still dropping and the character walking slower

*2026-11-26, the lead session, the owner's choice (« continuer sans vider, même plein tu peux quand même drop en combat, juste tu
marches moins vite ») over the lead's of ending the activity. Refines D79, D147 and D405. To make in a session of its own.*

- **Asked**: when the character reaches its pods' threshold, a profile may name neither the bank nor the guild's chest, and the
  activity goes on full; in game a character over its pods still gets the fights' drops and walks slower.
- **To make**: « Go on full » beside the bank and the chest where the harvest's, the hunt's and the dungeon's profiles say where
  what is banked goes, its texts in the three languages; the activity then never leaves for the bank on the pods, and its card says
  the character is full.
- **Rejected**: ending the activity, so that a profile's next step takes over, the lead's recommendation, which the owner passed over.

## D579: A profile may make its character a mule in fights: placed, ready, and passing each turn without acting

*2026-11-26, the lead session, the owner's choice (« Dans le profil ») of the lead's recommendation. Refines D244; D301's preferences
by character stay step 7h's. To make in a session of its own.*

- **Asked**: an option for the mules, characters that fight only to be there, passing their turns.
- **To make**: a flag in the profiles whose activities fight (follow, hunt, harvest, dungeon), read by the fight's play: the
  character placed as cautiously as now, set ready, each of its turns ended at once; the AI of its allies no longer counting it as a
  fighting ally.
- **Rejected**: a setting on the character, valid whatever the profile: it needs D301's preferences by character, a migration of
  the database and the character's card, step 7h's work.

## D581: The maps no way leads to are kept by the harvest for all its tours, through a restart after a failure and a sub-area drawn again

*2026-11-26, the lead session, after the owner's « Oui on fait ça ». Refines D571.*

- **Found** in the code while measuring the Pandawa's report (`artifacts/studies/panda-still.md`), never seen in game: D571's
  marks lived in each tour, so that a restart after a failure (D519), which draws the rotation again, and a sub-area dropped then
  drawn back began tours that sought the maps no way leads to again, one journey with no way each time.
- **Made**: `HarvestShutMaps`, one per harvest activity, holds the maps no way leads to for every tour it makes, and forgets them on
  the same facts as before: a level of the character or of a profession, the subscription, the zaaps known.
- **Rejected**: keeping them per host for every activity: another character's rules open other ways.

## D582: The map a group attacked and beat the harvest on stays out of its tours, and is not harvested stood on, until the character is within 20 levels of the highest monster it fought

*2026-11-26, the lead session, the owner's choices: « Oui on fait ça » of the lead's offer, then « ça faut pas que ça compte pour un
protecteur de ressource », the level of « le monstre le plus élevé », and « pas plus 20, mais - 20 ». Refines D481.*

- **Measured** on the Pandawa's journals of 25 and 26 November: 31 fights lost, every one on map 126092053 or 126093080, to groups of
  level 150 to 158, the character of level 83, its profile then without the safe level. After each loss the harvest regained its
  life and went back to the same maps (D481).
- **Made**: an aggression lost marks in `HarvestShutMaps` the map the fight began from (`FightEnd.FromMapId`, not the fight's own,
  D448), with the level that brings it back: the highest monster's the character fought less 20 (`FightEnd.HighestMonsterLevel`),
  the owner's figure of the levels below a monster from which it no longer attacks, not measured; the character's next level at
  the least, and when no monster was shown. Its tours leave it out, a journey does not stop on it for a resource, and, stood on,
  in the zone or out, the harvest moves on without harvesting it. A protector shuts no map: it comes of the resource harvested, wherever it
  grows. A zone left with only such maps ends as one no map of which can be reached, and restarts as the profile says.
- **Rejected.**
  - The protectors too, the lead's first making: the owner's correction.
  - The group's level, its monsters' summed, then the highest monster's plus 20: the owner's corrections.
  - The map the harvest's step began on, the lead's first making: a journey crosses several maps, and the aggression comes on one
    of them, which the review found.
  - Until the host stops, or for a time: a level is what may change the fight's end.
