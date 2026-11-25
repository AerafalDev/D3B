# Status

Where the project stands, on one page. Read it first: it says what exists, what is not measured, what comes next and what is
owed. The reasons are in [the decisions](decisions/README.md), found through [decisions-index.md](decisions-index.md); the steps and
their sessions are in [roadmap.md](roadmap.md); how the code is built is in [architecture.md](architecture.md). Whoever closes
a session updates this page, the roadmap and the index.

As of 2026-11-25, after D545 (the owner's asks: the parts slide open and closed as next's, the window's corners rounded on Windows 11), D544 (the owner's asks while checking the shell in the running app: the Details play, pause and stop a character, play all, the Characters view lists only those played, Planning, Combat and empty Variables gone), D543 (stage 4 of 4 of the studio's shell after next: empty views that guide, how to connect a character, a button making a route, a zone or a profile, the statistics' every session), D525 (the dungeons' rooms, the owner's go: in a room the character regenerates to its share of life, fights the room's groups and lets the server move it to the next room; a won room it was not moved from is left on foot for the nearest room not won; a fight lost leaves the dungeon for the run; the run ends with every room won or on the map the boss's win leads to, the exit next), D542 (stage 3 of 4 of the studio's shell after next: the editor's tabs, the one shown and the bottom panel's tab come back after a restart once the host runs, a tab whose subject is gone left out quietly), D520 to D524 (roadmap step 12's dungeons begun, the owner's ask and the owner's four choices: an activity of its own that reads the client's dungeon table and follows the server from room to room, a character alone first and the group after, every keyed dungeon by its replies' French words, the keyring or a key carried; then the owner's ask of several dungeons in one profile, entered in turn, those it cannot enter left out; the first pull request made in code and tests: the pack of format 14 holds the 187 dungeons and their keepers' replies, and the activity checks the level and the key, travels to the door and talks to the keeper; the rooms' chain and the exit next), D541 (stage 2 of 4 of the studio's shell after next: a status bar as VS Code's, the version and the relay on the left, the client and its data, the bots playing, the problems, the saving and the update on the right, each item opening what it names; seen in headless drawings), D540 (stage 1 of 4 of the studio's shell after next, under the owner's delegation: the search is also next's command palette, Ctrl Shift P typing its sign, next's command centre in the title bar, the views and each character's play, pause, stop and forget as commands; seen in headless drawings), D533 (the owner's ask, mounts before souls: a hunt profile's « Capturer des montures », off by default, the families ticked, the main tames a wild mount with its worn capture net's spell before any blow and spares it meanwhile, says when it wears no net, the followers keep theirs; nets, spells and stones measured in the client, none in the captures, the net's wearing left until a capture shows its request), D532 (stage 7 of the studio made simpler, the owner's ask: next's icons, Lucide 1.72.0 written by `scripts/icons.cs` under the app's keys), D531 (stage 6 of the studio made simpler, the owner's ask: next's layout, a thin title bar with the layout toggles, the activity bar's views alone, tabs on the frame, no tab at start, a character's column on the left), D530 (stage 5 of the studio made simpler, the owner's ask: next's dark style, VS Code's Dark Modern as discreet islands, dark only at the owner's word; seen in the running app on the owner's project), D518 and D519 (the owner's ask to end for good the miners left idle until the server's kick, four times in a week: a journey or a banking that fails climbs a bounded recovery ladder, tried again twice, the character put in order, a step aside, out of its map by the nearest exit, before it fails, D518; a run that fails, or makes no progress for 10 minutes, puts the character back in order and restarts its activity after 30 s doubling to 8 min, six times an hour at most, a failure no restart would change or a stop still ending it, by the profile's « Relancer après un échec », on for the harvest and the hunt, D519; tested on the fake game only, a harvest whose every request goes unanswered never silent more than 8 min 04 s), D517 (the owner's miner failed in a mine and was kicked for inactivity: a walk the server refused after showing it, with its confirmation in one batch, is refused, where D3B used the door from 19 cells away; and a bank's journey that says no path while a way to a bank leads from where the character stands is not travelled, so the unloading tries again instead of failing the run; tested on the fake game only), D516 (stage 4 of 4, the last, of the studio made simpler, under the same delegation: the statistics a tab of their own, the settings in a player's order, General, Bots, Files, Advanced, About), D515 (stage 3 of 4 of the studio made simpler, under the same delegation: the Library by kind, its folders on demand, the World's view making a route or a zone, the profile's header in settings), D514 (stage 2 of 4 of the studio made simpler, under the same delegation: a character's tab chooses its profile, its start on connection and plays, pauses and stops it; the home page is the characters' cards, a click opening the tab, a button playing or pausing), D513 (stage 1 of 4 of the studio made simpler after `next`, taken by the lead under the owner's delegation: an activity bar with Characters, Library and World as views of one side bar, Journal, Statistics and Settings at its foot, the bottom panel and the Details hidden until needed, the protocol an advanced tool, the View menu; seen through the headless drawings), D508 to D511 (a player's report of Kestrel's heartbeat late and the owner's ask to audit the fight AI: its odds, placement and plans searched on `FightSearches`, half the cores below the normal priority, off the thread pool the relay runs on, measured with 8 bots on 4 cores the pool's work items waiting 79 ms at most instead of 10 s, D508; the search's budget a limit of the whole search, its casts within three quarters of it, its reply within the whole and its passes sharing it, the first depth and two ends always weighed, replayed on the 24 November captures the searches 1,083 ms at most instead of 2,253 for a sum judged 0.07% lower, D509; a cast unanswered left out for the turn, and for the fight after a second turn, D510; a weapon's blow refused in a state that prevents fighting, which the server refused 74 times with text 372 on the captures of 22 to 24 November, D511; the rest of the audit, ranked, in `artifacts/studies/fight-ai-audit.md`, git-ignored), D506 and D507 (an audit of the harvest on the owner's four 14 h harvests of 23 November: the rotation goes to the nearest zone of the round not yet gone to, the zone just left last, where it went in the order the zones were left, the miner's journeys between zones 478 minutes, reckoned 35 % fewer map changes on the same rounds; and a journey stops on a map the tour went through already where a resource grew back, unless the tour left that element there, the farmer having passed about 1,800 such resources; the walks refused, the uses refused and D488's and D495's maps measured as holding), D505 (U3 of the next release's scope, the owner's: a run counts the fights its character won and lost while it went on, its deaths, and the experience and kamas they gave, kept in the run's record, migration `FightFigures`, and shown on the character's card, overall and by hour, and in the Statistics, by hour of the runs that fought; the card's kamas no longer the purse's change, which a bank's deposit made a loss; tested on the fake game only), D504 (U2 of the next release: each bot's journal and the host's problems written to `%LOCALAPPDATA%\D3B\journals` every second, a file a source and a UTC day, kept 7 days and 250 MB, the owner's numbers; "Save the journal" in the journal's toolbar; a report holding the character's journal of the last 24 hours from disk and the host's problems of that day as a part of its own, the player's folder hidden; tested on the fake game and on files the tests write, not yet run for a night in the app), D503 (the owner's report of characters summoning Chaferfus and a Pandawa drinking too often: *Invocation de Chaferfu*, a spell every character has, is a last resort whatever the breed, the Forgelance's lances aside; Saoul's share of the damage received, given with no rounds of its own, is dispelled with Saoul; and the states a character began its turn with stay held for every plan of the turn, the bot planning again before each action; on the owner's 4 captures of 24 November from 04:16, against `main`, the Forgelance's Chaferfu planned 0 times instead of 41, its damage foreseen 31,114 to 36,480, the Pandawa's Bombance 58 instead of 104 planning again as the bot does, its damage foreseen down 1.5%, every breed's up 3.5%), D502 (a player's suggestion: the client is found in the folder chosen in the settings, else where the launcher's `release.json` says it installed it, else in its default folder, for the host and the tools; tested on files made by the tests and on the owner's client, which is on the default path), D501 (a contributor's pull request, reviewed and completed by the lead: a hunt profile's flag, « Farmer les fragments d’anomalie », off by default, drinks a carried Minor Uchronic Elixir when its effect is missing or ends, through the object's use of D148, three uses not granted in a row ending the tries; the alterations read from the readable protocol, mapping format 25; tested on the fake game, seen in game by the contributor only), D500 (the owner's report of a Huppermage casting "Maîtrise des invocations" for nothing: every breed of the group did, the cast free and worth nothing but kept beside the search's best sequences as a set of states of its own, D255; a spell every character has that switches the summons' control is never cast, the control left as the player set it; on 14 of the owner's captures of 22 November, against `main`, the mastery planned 0 times instead of 25, the damage foreseen up for every breed, the Forgelance's 68,892 to 72,849; on the 4 of the night after, 0 instead of 153, the Pandawa's damage down 0.8%, the others' up), D499 (the owner's ask of a route discovering every zaap within reach the character does not know: an activity, « Découvrir les zaaps », walks to the nearest left at each step, the zaaps learnt shortening the next journeys, gives up one it cannot reach or is not taught on its map, and ends once none is left; tested on the fake game only), D498 (the owner's report of a Forgelance throwing its lance too often, striking earth around it while of fire, and passing turns: the lance stays on the map until it dies or a cast kills it, as 199 of the 221 turns of the captures that ended with it down measure it, instead of coming back as its thrower's turn begins; on 13 of the owner's captures of 22 November, against `main`, the Forgelance's plans of nothing 11 to 3, walks alone 14 to 4, damage foreseen 31,015 to 33,099, its earth blows' damage 692 to 402, no other breed moving), D497 (the owner's ask for more challenges: the AI plays for eleven more of the owner's list, Hardi, Élémentaire, Sans-cœur, Premier, Dernier, Élitiste, Tueur à gages, Tueur à vue, Cruel, Ordonné and Focus, each cast of the search judged against the fight before and after it, and the price of a challenge raised to three quarters of what keeps the reward a minute the same, the owner's choice), D496 (the owner's report of a Pandawa casting Bombance for nothing: a share or a reduction of the damage received a spell gives is held by that spell, so that a dispel takes it back, Saoul's 85% no longer kept once Sobre dispels it and stacked by the next cast; on 10 captures of 22 November the Pandawa's Bombance planned 81 to 75 times in 162 turns, the reply's life 3,851 to 3,923), D494 (the owner's report of a Huppermage of earth striking air to cycle its enemy's elemental states: a state set on an enemy adds to the side's blows on the next turn alone, no longer credited for the whole fight while the combination it sets off takes it off; on the same captures the Huppermage's damage foreseen 4,775 to 7,029 in 70 turns, Lame Astrale 31 to 6, Météore 24 to 50, Cycle Élémentaire 78 to 42, no other breed moving), D495 (the owner's two harvest reports: a resource is used only from its neighbours on its own floor on a map with stairs, the server having refused 124 of D3B's uses from 100 to 190 above a mine's ore and taken 427 from 40 or less; a mine's ledge the character's floor cannot walk to is come onto from the neighbouring map on the side it touches, 12 such parts on the pack; and a journey stays on the map it is about to leave where a wanted resource grew back since it came, 136 left so as the walk to the edge went on the captures of 18 to 22 November; tested on the fake game only), D493 (the owner's ask to handle the Huppermage's runes, then the Féca's glyphs and the Sram's traps: a rune set by the spell the Huppermage's elemental spell casts and struck only as its author triggers it, a trap set off by whoever comes onto it, a walk and a slide stopping there, and Transhumance triggering the Féca's glyphs, from the pack of 3.7.14.18 and the Bubble emulator; the captures' Huppermage set 376 runes and never triggered one; replayed, it plans Runification 7 times in 70 turns, its damage foreseen 2,517 to 2,573 and the reply's 262 to 337, the Féca's kills 230 to 231, no other breed moving), D492 (the owner's ask to check the Eliotrope's portals against the captures and Bubble: on the 217 captures of 18 to 21 November, 1,329 casts through portals, the cell they land on now foreseen every time, a tie between two portals broken by the angle, the share 2% a cell confirmed; a walk or a push that ends on an open portal is teleported onto the chain's last, 777 times in 818, now imagined by the AI, its search, its reply and the fight's state, the two portals closed until a turn begins), D491 (the owner's ask for an audit of the effects the engine left out, its first batch the owner's choice: the damage received reads the resistance to every element, a buff's reduction of 105 and the shares of 1163 a cast gives, and a cast moving its caster towards or away from its target slides it, measured on the 217 captures, the blows foreseen outside their range falling from 7,687 to 1,503, the ranked rest in `artifacts/studies/unhandled-effects.md`), D490 (the groups' stall of 2026-11-19 that D484 left: the master's gate waited without end for a follower missing; one missing five minutes, but fighting or healing, is now left behind and waited for again once it is back, the owner's choices, tested on the fake game only), D489 (the owner's report of a Forgelance casting its zone spell elsewhere than on its lance: the lance gives each spell it names its reach, 314 asks a taken cell, and a summon on a free cell asks its caster's conditions, measured on the 25 captures of 20 and 21 November), D488 (another player's report of a character stuck in a mine then disconnected: the harvest's walk to an ore crossed a map action's cell and the server moved the character on, 58 rounds of the mine's eight maps in 18 minutes on their captures; a walk out of fights now goes round the map's action cells, a harvest cut so leaves its map gone through, and a tour whose journeys arrive three times in a row with nothing done ends, saying so on the card), D487 (the audit's idle turns: a walk nearer the enemies that the judge weighs better before the reply wins a tie within a point after it, so that the characters far from the enemies walk instead of passing, the plans of nothing replayed on the captures of 20 and 21 November falling from 243 to 190, the damage, kills and reply as before), D486 (the owner's report of a Pandawa casting off its element: the bot's fight state no longer lets every buff go on a dispel or on a removal of a spell's buffs, so that the Pandawa keeps Sobre, which the server keeps, and casts its air spells; measured on the captures of 20 and 21 November, the farmer Pandawa in no state in 71 of its 85 turns before, none after), D485 (the owner's report from the group games: a character struck its allies, an Enutrof's and a Pandawa's zones taken knowingly, an ally's life above 60% worth next to nothing, and pushes into an ally never foreseen; a blow on an ally now costs what it takes, and a push a fighter stops strikes that fighter with half its own damage; on the three runs' 6,074 turns the plans' blows on the other characters fall from 12,035 to 2,581, the damage foreseen 1.5%), D484 (the second way to the inactivity kick a study read after D481: the harvest's and the hunt's steps waited without end for a busy character; busy a minute it is put back in order as after an interruption, still busy a minute later the activity fails saying what held, the owner's choices, not seen on the 216 captures of 18 to 21 November), D483 (a carry takes no fighter whose state keeps it from being moved or carried, the state effects 3 and 4 as Bubble reads them, nor one Enraciné or under Pesanteur, the owner's word, read in the pack and not measured, the owner's hint while auditing Karcham's refusals), D482 (a follower moves its whole stacks into its master's exchange in one request, a part of a stack by a move, measured on the owner's captures of 2026-11-21 evening where the client moved many at once, 31 requests each answered by one change; one move a stack took 54 s for 86), D481 (the owner's report of a farmer disconnected several times: a cereal's protector of level 30 beat the Pandawa of level 21 11 times in 14 on the captures of 2026-11-21, the third loss in a row stopped the harvest and the server closed the idle connection 30 minutes later; fights lost in a row no longer end the harvest and a loss is followed by the whole life regained, the owner's choices), D480 (the owner's ask to audit the captures of 20 and 21 November: a fighter's cooldowns the server gives merged spell by spell, not replaced, so that a Zobal no longer casts the mask it just took off, 167 of the AI's 184 refused casts; the rest of the audit measured with `scripts/fights.cs --live --details`, its idle turns left for a session of their own), D479 (the owner's report from game: the fight's pace before each action 0.3 s instead of 0.8 s and none before the turn's end, the owner's choice, measured on the day's captures where a turn took 3.7 to 5.2 s for 2 to 3.5 actions), D478 (the owner's report from a group game: a placement weighs only the threat of the enemies playing before the character's first turn, by the server's turn order, the owner's choice; on the 24 placements of the 07:46 UTC game the cell changes once, no enemy reaching a placement cell before a first turn), D477 (the owner's first game of D476: the followers the game moves no longer paused by their clients' walks, and their readiness left to the server, the master's alone said; the game measured following map changes), D476 (a group whose accounts are all subscribed, its followers of level 10 or more and its master leading the party, follows by the game's own options, the bot watching each follower and turning them on again, mapping format 24, measured on the owner's game of 05:53 UTC, the owner's choice), D475 (a follower joins its master's fight once the server listed it open, its options shown after the placement's cells, measured on the machine's captures, the release workflow of 0.9.5 stopped by the test that caught it), D474 (9f6, the Pandawa's carrying in the AI: Karcham carries an ally or an enemy and the AI throws it within the same turn, the throws' heals and blows on the fighter thrown imagined, a turn never ended carrying, the owner's three choices after the lead's study, the night's capture holding one carry and no throw), D473 (a fork reaches a row past its size, each row a step of its decrease: the Forgelance's Trident de la Mer, which the owner saw cast into the void, now foreseen striking two cells past the cell cast on, measured on the 92 captures of 3.7.14.18; the pull towards a cell and the casts that strike nothing left for sessions of their own, the owner's choice), D470 and D472 (the owner's run's reports: an AP or MP malus takes the points below 0 as the server keeps them, so that the Pandawa no longer casts Bombance for a phantom MP; within its spells' range and MP each cell nearer an enemy still counts, so that the characters walk instead of passing their turns, measured on the run's captures), D471 (a party holding the master and its followers kept whoever leads it, such as one formed by hand, a master with followers never closing its fight and opening one the server kept closed from its last, a follower joining only once its master's fight is open, the owner's report from game measured on the night's captures), D469 (the README written for players, building and maintaining D3B moved to `docs/development.md`, the owner's ask, superseding D204's layout), the app's 0.9.4 published with mapping format 23, D468 (an Eliotrope's portals in the AI: set, and cast through by every character of the bot when the judge prefers it, as the references model them, no capture holding a cast through one, the owner's choice after the lead's study; mapping format 23), D467 (an enemy of the reply holds every character of a group as dear as the fighter playing, as real monsters do, the first part of 10d, the reply's blows foreseen on the character in a group falling from 4.5 to 2.2 times those dealt, nothing changed alone), D466 (the AI plays for the 12 easy challenges, those of its own turns, while a turn costs no more than their bonus pays, a profile's option, Easy by default, the second of the three challenges' sessions, pack format 13), D465 (a thrown lance is out of the Forgelance's hand until its next turn, a measured rule the pack does not hold, the owner's question after D462), D464 (a refusal out of a fight leaves the walk held, and a follower whose change keeps failing from one cell steps aside, the owner's report of a follower stuck on a map until the server closed the group's connections for inactivity, its root cause measured on two runs' captures), D463 (`scripts/fights.cs --turns` and `--live` print their main counts by the character's breed, and the reply's blows against the enemies' alone and in a group, the guard of every change of the AI to come), D462 (the Forgelance's summons are not a last resort, its lances striking as they are thrown, the owner's choice after the study of the AI breed by breed), D461 (the bot reads its fight's challenges, their targets, their results and the proposals, the first of three sessions toward the AI playing the easy and the feasible challenges, mapping format 22), D460 (the AI weighs next turn's damage under 1 in a group too, so that it strikes now what it could strike later, the owner's report of characters passing their turns, D454's approach of 1.1 measured as its cause on the run's captures), D459 (a stack an exchange leaves is offered no more in that unloading, and a profile banking all but what is worn banks the group's food too, the owner's asks from game), D458 (a follower says it is ready 3.5 s after the exchange's last change, and an activity starting plays nothing before its run looks at the pods, the owner's reports from game measured on the captures), D457 (the master invites its followers at its first request, and a stack the bank refuses no longer ends the group's unloading, the owner's reports from game), D456 (no two bots of the host walk to one cell at once, a walk the server holds is let go only by a walk, and a follower never goes round while its master is one map away, the owner's report of followers lagging and one never entering the bank, its root cause measured on the night's logs and captures), D455 (the windows of a party invitation or an exchange the bot answered stay open, closed by hand, the owner's choice after a study), D454 (beside allies of its group the AI weighs the enemies' threat less and its next blows more, the owner's ask, figures to measure in 10d), D453 (a follower does not copy its master's walk to a door, and takes the door itself, the owner's report of a follower waiting before the bank), D452 (at the group's bank the master deposits all it does not wear first, and again when an exchange does not conclude, the owner's retest), D451 (a follower walks to an exit of its own as its master walks to the edge, and leaves from it with no walk, the owner's report of detours), D450 (the AI waits a second at most for its placement's answer, a swap with an ally left unread having cost each follower 3.8 s), D449 (a follower joins its master's fight from where it stands, and no two followers walk to one cell, the owner's retest), D448 (the followers into their master's fight: the master waits for them on the map its fight began from, they walk beside it, a held one steps aside first, the owner's retest), D447 (the followers copy their master's walks as they come, and the master no longer waits after each map change, the owner's report from game), D446 (a follower chooses the challenges' bonus its master's profile chooses, the bonus each character's own, measured in game), D445 (a follower no longer paused by its client's own "not ready" as it joins its master's fight, the owner's test in game), D444 (the harvest's tour goes into an interior while its sub-area lists a resource no map of it showed, so that it finds the mines), D443 (the bot plays the character's companion, such as the Astrub Knight, a player's report, mapping format 21), D442 (10c3, the group's bank: a full member takes the group to the bank, each follower gives the master what it carries by an exchange, the master deposits it with its own loot, mapping format 20), D441 (10c2, the master attacks once each follower's life is over its profile's share, the follower eating what only heals, else regenerating), D440 (10c1, the group's fights: a follower joins its master's fight by request, the master ready once its followers are placed; 10c in three pull requests, measured in game with 10b at the very end, the owner's choice), D439 (the harvest's journey stops on any map it crosses where a resource of the profile is offered, of the zone or not, a player's report), D438 (a profile chooses the challenges' bonus of its fights, the drops by default, mapping format 19), D437 (10b, the party by the bot, the "Follow" activity and the group moving in a chain), D436 (a profile that takes no zaap takes none on any of its journeys, the owner's report), D435 (10a, the party's protocol read from the owner's captures, mapping format 18) and D429 to D434 (step 10 begun ahead of step 7's measure in game, the owner's choice: groups designed in four sessions), D427 and D428 (client 3.7.14.18, its files released from 3.7.13.17's pins, the app's 0.9.2), D425 (the harvest's tour goes into another zone of a map, out and back through the world graph), D424 (a resource in another zone of its map is out of reach, no longer used from afar), D421 (7r6, a step of a list repeats and plays another list), D420 (the Pandawa no longer swaps Sobre and Saoul instead of striking), D419 (7r4, following a run), D418 (7r5b, the profile as an asset editor), D415 (7r5 part 1, the zones read by the harvest and the hunt), D417 (the harvest's tour weighs the character's level) and D416 (it leaves the houses of a town alone), D414 (hints on the world map), D413 (7r3, the Sequencer) and D408 (the fight AI after the players' reports, D407, summons a last resort; the home page, D406; banking by general settings, D405; D394, session 7d4, merged into 7r2's branch). App 0.9.9, client build 3.7.14.18, pack format 13 (published on the release `client-3.7.14.18`, 12 beside it), mapping format 25 (D501, published on the release `client-3.7.14.18` beside 17 and 20 to 24 on 2026-11-23, written from main's 7d6e9155, its 12,014,239 game frames of the owner's captures all understood).

## What exists

- **A bot that plays through the official client** (D23): the relay between the client and Ankama's servers, the readable
  protocol and its per-build mapping, the data pack extracted from the client (steps 1 to 3).
- **The run time** (step 4): one bot per character, walking, changing maps, using elements, banking, zaaps and the haven bag,
  activities, recovery.
- **Harvesting and the hunt** (steps 6 and 9): both choose their place by the character's levels and go through several
  sub-areas in turn (D391, D392); fights are played by one AI for every breed (D244), with the rules and effects of a fight
  measured on the owner's captures, and the AI's plans and fights replayed breed by breed (D463).
- **The desktop app as a studio** (step 7, up to 7d4, 7r2 and 7r3): dark, docked, seen as a game engine (D397: the world's map is the
  centre once a route or a zone is selected, with the characters, routes and zones on it, an Outliner and a Details panel; the studio opens on a home page, D406;
  an activity bar and one side bar since D513, the bottom panel and the Details hidden until needed, the protocol an advanced tool), the project's assets in files,
  behaviors and profiles as variants, the world's map drawn from the client's images, routes with their actions, a profile
  playing a list of routes and steps made in the app (D386), zones painted on the same map (D394), Undo and Redo in the route, the
  list of steps and the profile editors, and rows dragged or moved by the keys (D394). A route's steps are set in the Sequencer
  under the world (D413): a ruler of steps, a track a kind of action, a clip drawn across the steps it covers and stretched, moved,
  copied, deleted by the pointer or the keys, several steps selected at once, the Details setting a clip's values once for all its steps.
  The world's map also draws the client's own hints, zaaps, dungeons, workshops, banks and more, with a tick for each category (D414).
  A profile opens as an asset editor (D418): four categories, the rare settings folded under "Advanced · N changed" with an amber dot, the world's
  map beside the form to pick the zone or the sub-areas of its place.
- **Groups** (step 10, 10a and 10b): the party read from the server (D435); a follower plays "Follow", its master a
  character of the accounts page, and any profile leads: the master invites its followers and, before an action, waits for
  each on its map and in its party, whoever leads it, a party formed by hand kept (D471), the card saying whom for, five minutes at most but for one fighting or healing, then going on without it until it is back (D490); the followers keep the party and copy each of their master's
  walks as it comes, its exits too, each in its own haven bag as the master is in his (D437, D447), no two bots of the host
  walking to one cell at once, a door's cells taken in turn, and a follower never going round while its master is one map
  away (D456). In a fight (D440), a follower joins its master's by request, the
  master's fight is left to the party the server locks it to, never closed while followers are known and opened when the server
  kept it closed from the last (D471), a follower joining only once the server listed its master's fight open (D475), and
  the master says it is ready once its followers are placed.
  A group the game can follow, every account subscribed, every follower of level 10 or more and the master leading its
  party, turns the game's options on: the clients follow the master, the server joins its fights and sets them ready; the
  bot watches each follower, asks to follow again, takes it to its master's map itself after 10 s and joins a fight by
  request when the game did not (D476). Their clients' walks and map changes pause nothing, and each follower places itself
  and leaves its readiness to the server, its master's alone said (D477).
  Before acting the master waits for each follower's life over its profile's share, the follower eating or regenerating (D441).
  A member full takes the group to the bank, each follower gives the master what it carries by an exchange, and the master
  deposits it with its own loot in one opening (D442).
- **A versioned Claude Code setup** (D398): `.claude/` holds C# hooks that ask before a push or a `gh` write and refuse
  what the client produces, skills for C#, the studio's UI, tests, Git and the rest, and two agents. The hooks are tested in
  `tests/D3B.Hooks.Tests`; the write guard and the Git guard were seen refusing inside a session, the "ask" answers and the
  Stop hook's block not yet.
- **The zaaps' discovery** (D499): an activity with no setting, "Discover the zaaps", that walks to every activated zaap
  within reach the character does not know, the nearest first, and ends once none is left, each given up named on its card.
- **Published**: the installer and updates through Velopack (D171); the client build's pack, schema and mapping as the release
  `client-3.7.13.17` (D365), then `client-3.7.14.18` (D428).

## Not measured in game

- Wild mounts captured (D533): measured in the client 3.7.14.18 only, none of the owner's 74 captures holding a net or its
  spell; tested on the fake game. To capture in game: a hunt in the Bassin des Muldos with a universal net worn by hand, the
  taming cast and the fight won, so that the server's answers, the fight's end, the mount's item and the slot's request are
  measured, and the net's wearing built from them. In a group, a follower's or a summon's casts do not spare the mount.
  Souls (archmonsters, bosses) are studied, not built.

- Entering a dungeon and its rooms (D520 to D525): measured on the owner's captures of 21 and 24 November, played by hand in a
  party; tested on the fake game only, and the activity is not yet in the catalogue. To look at in game once the exit is made: a
  character alone, a keeper other than the four measured, the keyring's charge used up, a room left on foot, a lost fight inside.
- The miner's door refused on the bank's journey (D517): measured on the owner's capture of 24 November 20:58 UTC; tested on
  the fake game only. To look at in game: an automatic miner banking from a mine's inside, a door's walk refused, the journey
  tried again and the bank reached, no run failed still full. Left: a harvest journey whose only way failed once still says no
  path, and the guild chest's journey keeps no path as no chest.
- Never idle until the server's kick (D518, D519): tested on the fake game only, from the owner's captures of 23 and 24 November.
  To look at in game: a miner whose bank journey fails at a mine's door trying again and banking; a harvest that fails, or
  waits on nothing for 10 minutes, saying on its card that it restarts, and going on; no connection closed for inactivity
  while a harvest or a hunt runs. Left: a wait on purpose over 30 minutes, every zone growing back, still sends nothing; after
  six restarts in an hour the run ends and the character idles, saying why; the map's information is not asked again (D60).
- The fight AI off the thread pool and its budget a limit (D508, D509): measured with a harness of N bots planning at once
  on 4 cores and on the replayed captures, not in game; Kestrel's heartbeat not seen late again yet, and the plans of a
  machine slower than the owner's, which the casts cut at three quarters of the budget weaken most. A cast unanswered
  tried again the next turn (D510) and the weapon kept from a state that prevents fighting (D511), on the fake game and the
  captures.
- The harvest's rotation and resources grown back on the way (D506, D507): measured on the owner's four harvests of 23 November,
  the saving reckoned on the world graph, not played; tested on the fake game only. To look at in game: an automatic miner
  going from one zone of its rotation to the nearest not yet gone to, and its journeys between zones shorter than the
  478 minutes of 14 h measured; a farmer stopping, on its way through maps its tour went through, for the cereals grown
  back there, and not for one refused. Known: the protectors' fights, 66 to 202 a harvester in 14 h, 60 to 107 minutes, are
  the game's; the four runs of 23 November hold no harvests, banks nor fights in `d3b.db`, their professions only, not
  looked into.
- A run's fight figures (D505): counted from the fight's end the bot reads, and a death from the character's fighter dead as
  it ended, as `scripts/fights.cs --live` counts them; not yet seen on the card nor in the Statistics during a hunt in game.
- The client's directory (D502): the launcher's `release.json` read on the owner's machine, whose client is on the default
  path; a client installed on another drive, found through it, and the settings' « Dossier du jeu » with its folder picker,
  are not seen yet.
- The Minor Uchronic Elixir kept up (D501): seen in game by the contributor, on the pull request's first version, which sent
  the use without waiting for its answer; the lead's version, through the object's use of D148, is tested on the fake game
  only. The fields of the alterations' records and the grant are the contributor's measures: the owner's captures hold the
  snapshot, always empty, never a grant. To look at in game: a hunt with the flag on and elixirs carried, one drunk at the
  start when the effect is missing, none while it lasts, one as it ends, and none once the bag holds none. The mapping of
  format 25 is published on the release `client-3.7.14.18`, and the app's 0.9.9 reads it.
- The zaaps' discovery (D499): tested on the fake game only. To look at in game: a character of few zaaps given the activity,
  the server's list coming within 5 s of each zaap's map, a zaap's map with two zones, the fights met on the way, which the
  player plays unless the bot's switch has the AI play every fight, and the zaaps it gives up, which may be within reach.
- The Chaferfu a last resort and the Pandawa's drink (D503): replayed on the owner's 8 captures of 24 November against `main`,
  the bot's planning again after each action reproduced by a throwaway probe (git-ignored, `artifacts/bprobe/`); tested on the
  engine's fights only. To look at in game: a group's Forgelance summoning no Chaferfu while it has a blow or a lance to cast,
  and a Pandawa ending a turn drunk by one Bombance, no longer drunk, sober, drunk.
- The summons' control left as the player set it (D500): replayed on 14 of the owner's captures of 22 November and on
  the 4 of the night after, against `main`; tested on the engine's fights only. To look at in game: no character of a
  group casting "Maîtrise des invocations", and an Osamodas's or a Sram's summons still played as the client controls
  them.
- A Huppermage striking in its element and a Pandawa drinking once (D494, D496): replayed on 10 of the owner's captures of
  22 November with `scripts/fights.cs --turns`, against `main`; tested on the engine's fights only. To look at in game: a
  Huppermage of earth casting its earth spells, Cycle Élémentaire still free and planned 42 times in 70 turns; a Pandawa
  ending a turn drunk by one Bombance.
- The Forgelance's lance left on the map (D498): replayed on 13 of the owner's captures of 22 November, against `main`;
  tested on the engine's fights only. To look at in game: a Forgelance with its lance down recalling it (Épilogue on the
  lance, or a spell that kills it) and striking in fire, no turn ended doing nothing beside its lance. Left to the owner:
  Épilogue thrown with the turn's last AP on a free cell, striking nothing, planned 104 times in 227 turns (70 on `main`),
  the lance shielding the Forgelance from the enemies (their blows took 4,526 life from it in a capture, 5,572 from the
  Forgelance), whether one throw too many. Left: 39 of the 227 turns the probe judged better played than planned,
  the bot finding by planning again after each action a walk in pieces the first search misses; the recall in `Walked`
  and `Struck` (a trap or a glyph killing the lance) untested, only the cast's.
- A mine's floors and a resource grown back (D495): measured on the 126 captures D3B played of 18 to 22 November and on the
  pack of 3.7.14.18; tested on the fake game only. To look at in game: the miner on the mine of 193331717 no longer refused
  its ore from above the cliff, then the tour going to its west neighbour and coming back onto the ledge (the host's log
  "came onto map ... from side 4"), or saying it could not; a resource growing back as the character walks off its map,
  the log "stays on map ...", and the harvest. Not measured: whether the server always places the character on the cell
  straight across a ledge's edge (46,900 of 52,658 scrolls did), and the 21 neighbours of 41 to 99 above an element, left out.
- The far walks winning a tie after the reply (D487): replayed only; the owner is to say whether the characters still pass
  their turns far from the monsters. Left: 33 far turns a walk outside the 3 best before the reply would win, 55 where no
  walk beats standing before the reply.
- A follower left behind (D490): read in the code, the master's gate waiting without end; the captures of 18 to 21
  November hold only D464's two stalls, none after it; tested on the fake game only. To look at in game, only when it
  happens: the master's journal saying "Goes on without …", the group going on, the follower coming back and waited for
  again. Known: a follower left behind walks alone and fights alone what attacks it on its way.
- A dispel keeping the buffs the server keeps (D486): measured on the machine's 216 game connections of 3.7.14.18, the
  server ending one by one the buffs a dispel takes and keeping those not dispellable, a Pandawa's Sobre among them; the
  turns of 20 and 21 November replayed, the Pandawa planning its air spell where it planned earth and fire, no other
  breed's damage, kills or reply moving. Tested on the bot's fight state only. To look at in game: a Pandawa against
  monsters that dispel, still casting its element's spells after the dispel, and the farmer's fights against aggressions.
- The blows on the character's allies (D485): measured with `scripts/fights.cs --turns` on the owner's three group runs of
  2026-11-20 and 21, the plans' blows on the other characters 12,035 to 2,581 for 13,631 the turns played took; tested on
  the engine's fights only. To look at in game: a group with an Enutrof and a Pandawa, their zones no longer cast on a
  monster beside an ally at full life, no push of an Iop, a Zobal, an Eliotrope or a Forgelance into an ally, and
  `scripts/fights.cs --live` counting the life their casts take from the team ("life its casts took from"). Left: the
  Forgelance foreseen striking its lances where the server announced little, a push into an ally of the caster's side
  taking a quarter 360 times in 756, not explained.

- The exchange's stacks in one request (D482): measured on the owner's captures of 2026-11-21 evening, the client moving
  many stacks in one request into an exchange with a player; tested on the fake game only. To look at in game: a mule
  emptied in about a second of moves, then D458's 3.5 s, the exchange concluding; the readiness's step after a transfer of
  many stacks is the lead's reading, not measured; a mule carrying an object bound to the account, whose transfer the
  server may refuse whole, the follower then moving its stacks one by one.
- Fights lost in a row while harvesting (D481): measured on the owner's captures of 2026-11-21, 14 protectors' fights, all
  one scarecrow of level 30 against a Pandawa of level 21, 11 lost, each next one at full life; tested on the fake game
  only. To look at in game: a harvest losing three fights in a row going on, the character waiting for its whole life after
  each (the card's "Regaining life after a lost fight"), no disconnection for inactivity. Known: an activity that fails still
  leaves the connection idle until the server closes it, the owner's choice; nothing reads the energy each loss costs, so a
  long run of losses would make the character a ghost.
- The walk round a map action's cell and the tour going round (D488): measured on another player's two captures, the
  loop 58 rounds through the eight maps of a mine of sub-area 525 (168034304 to 168034312, 168165888, 168166912,
  168167424), the ore 507630 of 168034306 never reached as its walk crossed cell 341; tested on the fake game only. To look
  at in game: a miner harvesting that ore, walking round cell 341, and the tour then leaving the mine for the sub-area's
  other maps. Not measured: whether every map action's cell acts on a walk that only crosses it, and one whose criterion the
  character fails; why the server closed that connection 20 s after a walk it left unanswered, nothing it sent saying so.
- The wait for a busy character bounded (D484): read in the code, never measured, the 216 game captures of 18 to 21 November
  holding no step stuck on a busy state; tested on the fake game only. To look at in game, only when it happens: the host's
  log warning "busy ... for a minute at a step's start", then the activity going on, or failing with "Still ... after two
  minutes". The groups' stall of 2026-11-19 (a master waiting 30 minutes for a follower whose map change the server
  refused, D464) is another wait, bounded by D490.
- The cooldowns merged (D480): measured on the 130 game connections of 2026-11-20 and 21, the 167 refusals of the Zobal's
  masks on a cooldown the bot now holds and none of the 12,605 casts the server announced on one; tested by a unit
  test of the fight's state, not on the fake game, nor replayed with `--turns`. To look at in game: a Zobal changing mask,
  never refused the mask it took off in the same round. Left from the audit: 17 refusals of the Pandawa's Karcham unexplained, and 179 of the AI's 238 idle turns
  ended with MP left, the nearest enemy 5 cells away or more and no spell landing, 167 of 224 after D472.

- The fight's pace (D479): 0.3 s before each action, none before the turn's end, tested on the fake game only. To look
  at in game: the turns of a group fight about 1.5 s shorter for three actions, the client still showing each action
  before the next, no request the server refuses for coming too soon.

- The placement by the turn order (D478): measured with `scripts/fights.cs --turns` on the owner's game of 2026-11-20,
  07:46 UTC, 24 placements in a group, the cell changed once, no enemy reaching a placement cell before a first turn. To
  look at in game: a fight whose placement cells are within the enemies' first reach, a character playing after them
  kept out of it and one playing first placed where it strikes.

- The game's own following (D476, D477): measured on the owner's games of 2026-11-20, by hand at 05:53 UTC on one map, then
  through the app at 07:12 UTC: the clients walking and changing maps after their leader, each follower on its master's map
  within a second, the server joining the fight and setting the followers ready; two followers paused by their clients'
  walks then, which D477 fixes, tested on the fake game only. Not measured: a door, a zaap, a member whose own option is
  off, a refused request to follow. To look at in game: four subscribed characters of level 10 or more, the bots
  launched, the followers following the master across several maps, a door and a zaap, joining and ready by the game, and
  one whose following is turned off by hand going back to its master's map and following again. The mapping of format
  24 is published on the release `client-3.7.14.18`.
- The party kept and the fight's lock (D471): measured on the owner's game of 2026-11-20 (00:32 to 00:38 UTC), each follower
  leaving the party a follower led, the master fighting alone and closed, and the server keeping that fight's lock into
  the next, the followers' joins refused; tested on the fake game only. To look at in game: four characters in a party
  formed by hand, the bots launched, no follower leaving it, their options turned off, and no fight locked before they join.
  Not explained: for 1 min 45 s the master's gate held no follower, their activities not in the host's groups; the app's
  log of such a run would say why.
- A follower the server holds (D464): measured on the owner's group runs of 2026-11-19 (06:56 and 18:47 UTC), one follower
  asking for its master's map from its exit every 8.5 s for 30 minutes, each refused, until the server closed the group's
  eight connections for inactivity; tested on the fake game only. To look at in game: a group playing for hours, no
  follower stuck on an exit, no disconnection for inactivity.
- One cell each (D456): **seen in game** by the owner on 2026-11-19, main 9b7561f1, a master and three followers through the
  app: every follower kept up, map after map and through the bank's door ("c'est bon, ils suivent tous").
- The invitation at once and a stack the bank refuses (D457): the invitation **seen in game** on 2026-11-19, main 31a47c6a, the
  master inviting its followers at its first step; the master's deposit refused, then its followers' exchanges taken, seen.
- The exchange's pace and the pods seen first (D458): **seen in game** on 2026-11-19, main a752a945: three mules emptied, every
  exchange concluding at once ("ça marche"). The stacks went one by one (54 s for 86): one request since D482.
- The bank's rounds and the food (D459): the owner's report from that test, two empty exchanges after each mule's, 49 s in all,
  measured on its captures; tested on the fake game only. To look at in game: one exchange a mule, and with "Tout sauf
  l'équipement" the mules' and the master's food banked.
- The companion (D443), a player's report of the Astrub Knight left unplayed: the bot plays the character's companion as a
  summon it controls, with the spells the server lists for it, tested on the fake game only and read from the client's
  schema alone, no capture holding a companion. To look at in game, the owner's fight with the Astrub Knight, captured:
  whether the server gives the client the companion's turn and lists its spells, whether the companion casts and walks as
  the AI sends, and what the companion's string and two int32 hold. The mapping of format 21 is published on the release
  `client-3.7.14.18`.
- The mines (D444): the owner's report that the automatic harvest gathers about as much in every profession but the miner's, much
  less, read in the code and measured on the pack of 3.7.14.18 (42 maps of Amakna's mines are interiors D416 left out until a bot had
  learned their ore), fixed and tested on the fake game, not seen in game; neither the report's numbers nor a database that learned ore
  were on the lead's machine. To look at, on one database: "Auto miner" and "Auto lumberjack" 15 minutes each, the ore harvested, the
  mines entered (Mine Hable from the Carrière d'Astrub), the houses entered, and the automatic profile's harvests by profession.
- The challenges' bonus (D438): the option of the hunt's and the harvest's profiles, Drops by default, sent by the AI during the
  placement once the server answered the client's own choice, tested on the fake game only. To look at in game: a fight alone with
  each value (the bonus shown in the challenges' panel, the log line "chooses the challenges' bonus"). In a group, seen in game on
  2026-11-18: the bonus is each character's own, the followers' clients choosing the experience; each follower now sends its master's
  profile's choice (D446), not yet seen in game.
  The mapping of format 19 was never published: format 20 (D442), which holds it, is, on the release `client-3.7.14.18`.
- The challenges read (D461): measured on the captures of 2,980 fights and tested on the fake game; the log lines "the fight's
  challenge" and "challenge ... won" not yet seen in game. Not measured: a fight with more than one challenge, a group's
  challenges, the state's two other values. The mapping of format 22 is published on the release
  `client-3.7.14.18` (the owner's go, 2026-11-19).
- The easy challenges (D466): tested on the engine's fights and the fake game, and the search keeping each measured on the
  captured turns (`scripts/fights.cs --turns`), not seen in game. To look at: a hunt with the profile's default, the log lines
  "leaves challenge" and "challenge ... won" against the challenges' panel, a Statue or a Zombie left by a melee class, a
  Prudent kept. Not measured: Nomade's partial escape, the MP a spell gives in the turn, whether the server counts the summons
  among the allies of Collant and its kin. The pack of format 13 is published on the release `client-3.7.14.18`
  (the owner's go, 2026-11-20), extracted from main's a6150290, 111,906,814 bytes.
- More challenges (D497): tested on the engine's fights and the fight's state, its re-reading before each plan of a turn on
  its own and not through the fake game, replayed on the 83 captures of 18 and 19 November, not seen in game. To look at: a fight of each, the log lines "leaves challenge" and "challenge ... won" against the challenges' panel,
  an Élémentaire kept across turns, a Tueur à vue's next target. Not measured: whether the server counts a summon's, a poison's
  or a trap's blow for the fighter that set it, a heal at full life under Sans-cœur, an enemy's summon as a target or among
  Cruel's and Ordonné's levels, how the server names Tueur à gages' next target. Left of the 43: Économe, Imprévisible,
  Blitzkrieg, Barbare, Mains propres, Intouchable, Survivant, Duel, Chacun son monstre, Partage, Les petits d'abord,
  Deux pour le prix d'un, Doume, Miroir, J'en ai fait un pin's, Conquérant, and the dungeons' Duo, Trio, Quatuor and Chrono, none
  asked; and choosing among the proposals.
- **Step 10 is measured in game only at the very end** (the owner's choice of 2026-11-18): 10b, the challenges' bonus in a
  group and 10c are looked at together once 10c is made, each tested on the fake game only until then.
- 10c1, the group's fights (D440): seen in game by the owner on 2026-11-18, each follower paused from the second fight by its client's own "not ready", fixed by D445 and not yet seen again; otherwise tested on the fake game only. To look at with the owner's two characters: the master
  attacking, its follower joining by request as the fight shows on its map, the master refusing spectators but no longer
  closing the fight, the follower setting no option, the master ready only once its follower is placed (the log line "waits
  for its followers ... to be placed"), a lost fight and the chain gathering again. Not measured: whether the server takes a
  follower's join once spectators are refused, and how long a placement with followers lasts.
- 10c2, the life before an attack (D441): tested on the fake game only. To look at: a follower under the master's share
  ("Heal under") eating the food it carries that only heals, or regenerating, the master's card saying "who regains its
  life" before an action (a harvest, a door, the bank, a zaap, the haven bag, and before the walk to a group it attacks,
  D447) until the follower's life is over the share; its walks and map changes no longer wait.
- 10c3, the group's bank (D442): tested on the fake game only. To look at: a follower full taking the group to the bank,
  its exchange with the master (asked, accepted, each stack moved, both ready), the step the server takes (3 after 3 moves
  in 10a's capture, the lead's reading), a master whose pods fill depositing between two exchanges, the master's deposit of
  what it was given and of its loot (a gift merged into its own stack moved in for its quantity alone, unmeasured in the
  bank), the follower keeping its food, the exchange's windows on both screens. The mapping of format 20 is published on the
  release `client-3.7.14.18` (the owner's go, D442).
- The Pandawa's carrying (D474, 9f6): the pack's Karcham and throws, the night's capture of 2026-11-20 holding one carry (its
  states measured, the carrier then idle two turns) and no throw, the references' model for the rest. Replayed on the 92 game
  connections, the Pandawa's 115 turns plan 2,566 damage instead of 2,534, 51 kills instead of 48, the reply taking 644
  instead of 704, Karcham planned 27 times instead of 7; no other breed moves. Tested on the imagined fight only. To capture
  and see in game: a Pandawa carrying then throwing an ally and an enemy, by Karcham and by a throw spell, the cell it lands
  on, the throw's heal or blow on the thrown, and the carried ally's own turn. Left: the carried's turn, a carrier pushed,
  Porteur's power boosts. Since D483 the AI no longer imagines carrying a fighter whose state holds the state effect 3 or
  4, as Bubble reads them (Lourd, Indéplaçable, Emprisonné), nor one Enraciné or under Pesanteur, the owner's word; no
  capture holds such a carry, the 17 refusals of Karcham of 20 and 21 November (D480) on targets
  in none of those states.
- The Eliotrope's portals (D468, D492): measured on the 217 captures of 18 to 21 November, 1,329 casts through portals,
  937 the Eliotrope's and the others its allies': the cell as the server named it 1,329 times now that a tie between two
  portals is broken by the angle (1,313 before), the share 2% a cell fitting the damage best (Bubble's 2 more fits less). A
  walk or a push that ends on an open portal is teleported onto the chain's last, 777 times in 818, which the AI did not
  know: now imagined, in the search, the reply and the fight's state, the two portals closed until a turn begins.
  On the 8,771 turns replayed, the Eliotrope's plans foresee 2.4% more damage over two turns and 488 kills instead of 477,
  9 fewer plans doing nothing; the Pandawa, the Zobal and the Forgelance within 1%, the others within 0.2%. Not measured in game: the AI walking onto a portal on purpose. Left: the closing effect 1183, the 2% more
  damage each of the Eliotrope's casts through portals gives it for 3 turns (spell 14631), the action 1181's payload, the
  enemies' casts through portals, none captured. The mapping of format 23 is published on the release `client-3.7.14.18`
  (the owner's go of 2026-11-20), and the app's 0.9.4 reads it.
- The Forgelance's lances (D462): its summons searched from the first search, as an Osamodas' are, the owner's choice after the
  study of the AI breed by breed. Replayed on the captures of 18 and 19 November, its 221 turns plan a third more damage
  (5,885 instead of 4,439), 171 kills instead of 120, and the reply takes less (1,546 instead of 1,907); no other breed moves.
  Tested on the imagined fight only; to see in game: a Forgelance throwing its lance. The other breeds' spells that strike and
  summon (the Féca's, one of the Eniripsa's and of the Pandawa's) stay a last resort. A thrown lance is out of its hand until
  its next turn (D465, the server's own rule, measured on the captures; its return as the next turn begins undone by D498): the replay now plans 5,676 damage instead of 5,885,
  162 kills instead of 171, no second throw the server refuses; to see in game: one throw a turn, then what it may cast.
  Trident de la Mer's fork reaches two rows (D473), measured on the captures: its 21 blows on the second row are foreseen
  and sought, Trident planned 61 times instead of 27, the Forgelance's damage planned 6,493 instead of 6,213; to see in
  game, a Trident striking two cells past the cell cast on. Left, each for a session: a pull towards a cell imagined
  stopping beside it where the server takes the target onto it and past it (728 pulls, every breed's), and casts planned
  that strike nothing (7 of the 61 Tridents). The spells cast on the lance (D489, the owner's report of 2026-11-21): the
  lance gives 13 of the Forgelance's spells a range of 1 to 63 and asks them a taken cell, and casts their own spell around
  it; replayed on the 25 captures of 20 and 21 November, the casts planned on the lance 24 to 112, Épilogue on a free cell
  26 to 0, the Forgelance's empty plans 66 to 52 and kills 179 to 184, the Pandawa alone of the other breeds moving, by 4
  casts; to see in game: Effondrement cast on the lance from afar, striking around it. Found and left to the owner: in
  those captures the lance stayed on the map into its thrower's next turn 66 times of 81, against D465's return; made so
  in D498, the lance staying until it dies or a cast kills it.
- The AI in a group (D454, D460): a little bolder beside allies, the enemies' threat weighed 0.75, next turn's damage as alone
  (0.9). D454's approach of 1.1 left 15 turns of 87 empty in the owner's group run of 2026-11-19, main a752a945, a monster in
  reach each time; replayed on its captures, these weights plan a blow on all 15 and twice the damage. Tested on the fake
  fights only; to see in game: the characters hitting when they can, and fewer blows of another element. 10d sets the threat.
- The group's reply (D467): the imagined enemies choosing among a group's characters without the fighter playing's life
  counted twice; replayed on the 83 captures of 18 and 19 November, the blows foreseen on the character in a group 5,237 for
  2,360 dealt (x2.2, x4.5 before), on its allies 28,739 for 13,395 (x2.1), the solo fights unchanged. Tested on the fake fights
  only; to see in game: the characters of a group a little less on the defensive. Left for 10d: the allies' spells and their
  turns in the reply, which keep the whole team's foreseen at twice what it takes.
- The group's bank (D442, D452): seen in game on 2026-11-19, the master full taking its followers' objects, two exchanges never
  concluded, the master still full after banking; now it deposits all it does not wear first and again when an exchange does not
  conclude, tested on the fake game only; a follower no longer copies its master's walk to a door (D453). Left: the invitation and exchange windows the bot answered, left open and closed by hand (D455: closing one is harmless, measured).
- The master's exit taken straight (D451): seen in game on 2026-11-19, a follower beside the exit, one step more unconfirmed, then a
  detour by two maps; now it walks to an exit of its own and leaves from it, tested on the fake game only.
- The followers' readiness (D450): measured slow on 2026-11-19 (6 s when a follower moved, 3.8 s of it waiting for a swap's answer the
  state does not read); the AI now waits 1 s at most for its placement's answer, tested on the fake game only.
- Joining from where they stand (D449): seen in game on 2026-11-19, the followers walking beside their master to the monsters, and
  three to one cell, two held; now they join from where they stand and walk apart, tested on the fake game only.
- The followers into their master's fight (D448): on the owner's retest, the master never waited (the fight's map is not the one it
  began from) and the followers, walking to their master's very cell, were held by the server; fixed, tested on the fake game only.
  To look at: every follower joining, the master's "waits for its followers ... to be placed", the followers beside the master.
- Following in real time (D447): seen too slow in game on 2026-11-18 (the master waiting 29 to 38 s on each map for three
  followers in a chain); the followers now copy each walk of their master as it comes and the master no longer waits after a
  map change, tested on the fake game only. To look at: the followers walking with their master, taking its exit just
  behind it, one that misses a walk catching up by its own paths, and the master waiting only before an attack, a harvest,
  a door, the bank, a zaap or the haven bag.
- 10b, the group (D437): tested on the fake game with two and three bots, partly seen in game (below). To look at with the owner's
  two characters, each in its own client through the app: the invitation by name and its acceptance, a stranger's
  refused, the two options turned off (which of the two is which still unmeasured, and whether the client's window turns
  them on again at the next party), the master waiting on each map until its follower arrives and the card's "Waits for",
  the follower taking the same exit, a zaap and the haven bag followed, a follower's client closed and opened again.
  Seen in game (D437): the invitation, its acceptance and the options turned off, the master acting once its follower
  is in its party, the chain and a client closed and opened again; the client's window for the invitation stays open,
  D3B's acceptance not closing it (closed by hand). A follower that went round, its client not following its first walk
  after the master left, now leaves once the master is placed and tries the same way again: to see again in game.
  Following the leader, the game's third option, is still neither measured nor mapped (D435, D437): a follower of level 10
  and subscribed that has it on is not turned off; a capture of one turning it off is owed before 10d.

- The profile's zaaps (D436): a profile with "Take zaaps" off walking to its place, back and to the bank, tested on the fake
  game only, not yet seen in game.
- Client 3.7.14.18 (D428): its mapping made from pins alone, equal pair by pair to 3.7.13.17's from captures, not yet read on a
  capture of this build; the client following D3B's actions (D60) not measured again on it.
- The harvest on the way (D439): a player's report ("it harvests nothing until it gets to the zone"), read in the code, tested on
  the fake game, not seen in game: a journey back from the bank or between two sub-areas of the rotation stopping on the maps it
  crosses where the profile's resources stand, and going on once they are taken or refused.
- The harvest and the zones of a map (D424): a player's report ("it tries but cannot, unless it stands in front of the resource or comes back from
  another side"), read in the code and measured on the pack (864 maps with more than one zone, 3,673 element and zone pairs on 607 maps), fixed and
  tested on the fake game, not seen in game: that the server refuses a use from a cell two steps away, and that the tour no longer fails on those
  maps. To look at in the client: maps 0, 3333, 3845, 133890, 134403 or 134404, a character in one zone and a resource beside the other's cells.
  D425 (the tour going to the other zone through the world graph, out and back by another map, once per tour) is tested on fake graphs and a fake
  game with a map of two zones, not seen in game. To look at, on the same maps: that the character leaves the map and comes back into the zone of the
  resource (the landing cell's zone is the graph's `toZone`, measured only on the pack), that the vertex of the other zone is the one the character
  stands in once there, the number of map changes the way takes (the cap of six is the lead's reckoning), and the debug lines "the tour will go to
  zone" and "leaves zone ... alone" in the log. A map whose other zone no way leads into is left alone, so is one the graph does not hold.

- The sending (BR4, D410) was run for real from a throwaway tool with the production sink: posts of 0.1 to 20 MiB of random bytes were taken by the owner's
  webhook and 21.5 MiB and more refused (the limit is Discord's, depends on the server's level and may change: a refusal is a result, and the app says to hand
  the file over); a cleaned report of a real capture came back from Discord byte for byte and read by `ReportReader`, with no mention made of an `@everyone` in its
  message; every post was deleted. Not done: a send from the app's dialog, the asset read from GitHub by the app (it is published), a rate limit (the webhook's own limits were
  not reached), a webhook revoked.
- The journal kept on disk (D504) is tested on the fake game, on files the tests write and in a headless window: not yet seen
  over a night in the app, nor its size a day, which D504 estimates at 3.6 MB a bot; the report's 24 hours of journal and the
  host's part not yet sent to Discord.
- The report's dialog (BR3, D410, D411) is tested on view models, with captures the tests make, and in a headless window drawn in both languages
  (form, preview, written), and, since the fix after release v0.9.0, through the real `MainWindow` (Help, Report a bug, form, Preview). v0.9.0's Help item opened
  nothing: the window's switch over its dialogs had no case for the report. Now one table (`DialogKind`) registers and shows each dialog, and a test
  fails when a dialog view model (`IDialogViewModel`) has no row. The dialog sends with one button from the preview, and offers the code and the folder only when the send fails; it shows each character's portrait. The fix reaches players with the next release. Not opened by the owner from the Help menu, not tried on the capture of a real session through a running host
  (which bot, which connection, which capture, the profile and the assets it reaches), and its look is not judged against a mockup.
- The cleaner (BR2, D423) was run on 48 of the owner's real captures of 3.7.13.17: 45 were cleaned with no ticket, secret or name left in any
  output and 3 were refused for want of a character list the translation reads (the tool fails closed); on one of the largest, whose fights `scripts/fights.cs` replays with the same result as the original. What it leaves is said in D421
  (other players' names, numbers, timestamps, the GUIDs of unknown meaning, a name under 6 bytes in a frame too deep or cut short to walk); a
  next build's own opaque strings are found by shape only if they keep it, and the copy is searched for every secret it found before it is returned.
  The report is not yet made by the app, so no report was sent anywhere. `scripts/report.cs --replay` ran on a report of a real capture.
- Recording by default and its retention (BR1, D409) are tested on temporary files, the relay of the tests and view models, and the settings
  and the home page's notice in headless windows, not seen by the owner in the app and not run on a client. The first start of the host after
  the change deletes the game's captures older than 7 days or beyond 1 GB: the owner's 153 captures, which `scripts/fights.cs` replays, would
  go, so he sets `Network:Captures:Retention:Days` to 0 (or copies `captures\game`) before it. The 7 days and the 1 GB are D409's, the lead's reckoning.
- Repeats and sequences as steps (D421, 7r6) are tried on the fake game, view models and headless windows, not on a client and not by the owner
  in the app: a route followed three times (the journal saying its steps in the first pass only), a loop followed until pods at least 90 % (a lap
  long enough overshoots, the condition being read between passes), a list played inside a list with a fight in the middle of its second step, a
  block that holds a route, the flyout of the repeat token and of the condition (the headless window opens no popup), and how the tokens read under
  each step. The list is in D421; it joins the measure owed below.
- Following a run (D419, 7r4) is tried on the fake game, view models and headless windows, not on a client: whether the position told is the
  position lived with real travel (zaaps, `Busy` waits, a map with no path), a fight or a bank cutting the route and the run resuming on the
  same step, a long action while the playhead stays on its step, two accounts on one route, 250 ms against a map every three seconds. The
  list to look at, one by one, is in D419; it joins the measure already owed below.
- The hints on the map (D414) were seen only as headless renders of the real pack and tiles at four zooms, not in the app by the owner: how
  the pictures read, whether the thresholds and priorities (the lead's reckoning) thin them as they wish, the name over the hovered one. The
  pack of format 12 is extracted on the development machine and not published: the app of this change needs it (`scripts/release.cs --publish`,
  the owner's go).
- The zones as a place (D415) are tested on the fake game and in the view models, not in game and not seen by the owner in the app: a harvest or a hunt through a zone's maps, the field that picks it and the words under it, and what the French "Région" (the game's area, once "Zone") reads like to them.
- The profile as an asset editor (D418) is tested on view models and headless windows, not seen by the owner in the app: how the four categories and the "Advanced" folds read (the marks of each rare setting are the lead's application of D415's list), the map beside the form (its tiles were not drawn in tests: the source was empty), a click on the map against a drag, and the narrow band. A sub-area clicked outside the region chosen only raises the field's "not within" problem.
- The Sequencer (D413) is tested on view models and in the headless window, not yet seen by the owner in the app: how a drag feels
  on a long ruler, how the tracks read at the bottom panel's height, whether it is what they mean by practical; no screen reader was
  run on its grid.

- The routes' actions that talk to the game and the profiles playing a list of steps (7d3c, 7d3d): tried on the test game
  only; a real client with the owner is to do.
- The home page (D406) is tested on view models and a headless window, not yet seen by the owner in the app: its look and whether the six recent assets and the characters are what they expect.
- 7d4's editors (undo, dragging, the zones' tools) are tested on view models and headless windows, not yet by the owner's hand
  in the app: how a drag feels, the focus after a move by the keys, the zones painted at several zooms.
- The owner's reports from game answered since 2026-11-06 (D387 to D392): the hunt attacking every group, the tour leaving
  interiors out, the AI fleeing less, the rotation through ten sub-areas and the professions in turn. The numbers 5, 10, a
  quarter and 50 of D391 and D392 are the lead's reckoning.
- Banking by the general settings (D405): the share of pods and the zaap rule are tested on the fake game and in the app's
  view models. A bank journey that outlives a refused zaap is the lead's reading of a player's report (Incarnam to Astrub, 1,500
  kamas the character lacked), not seen in game; the text the server answers to a zaap that cannot be paid, and the cost of a
  zaap leaving Incarnam's world map, are not measured.
- The harvest leaving the houses of a town alone (D416) and weighing the character's level (D417): a player's report ("I still go into
  all the houses in Astrub"), read in the code and measured on the pack and the owner's learned graphics, tested on the fake game, not
  seen in game. A mine whose graphics no bot has learned is no longer discovered by a tour; an outdoor map is never left out for the
  level, only gone to later.
- The players' reports answered by D407: the new curve of the character's life (above 60% a point is worth a quarter) and the
  plan with the fewest actions among equals, replayed on the captures but not played in game; what the server sends of an
  invisible fighter (no capture holds one: the AI keeps its last cell and `Combatant.IsVisible` is never unset); the damage of a
  long push against a wall (the captures hold pushes of 1 to 4 cells, the pack's *Panache DoPou* pushes 10); the players'
  *Chafer* and *Arakne* summons, which no breed of the pack casts.
- The owner's run of 2026-11-20 (D470, D472), measured on its captures and tested on the fake fights only; to see in game: the
  Pandawa with no MP left no longer casting Bombance over and over, and the characters walking to monsters out of their
  spells' sight instead of passing their turns. Replayed on the 92 captures, the empty plans fall from 213 to 157, the damage foreseen as it was.
- The Pandawa swapping Sobre and Saoul with Bombance or Picole instead of striking (D420): a player's report, reproduced on the imagined fight of the pack's spells and fixed in the judge (a state held as the search began is no gain), not seen in game: no capture holds a Pandawa casting either, so what the server sends back of the swap (the two states, the AP given back, state 1) is read in the pack. The player is to say whether it strikes now.
- The Huppermage's runes, the Sram's traps and the Féca's Transhumance (D493): modelled from the pack and the references
  and tested on spells built as theirs; the captures' Huppermage set 376 runes, of mark type 4, and never cast Runification,
  their Féca no glyph, and no Sram of the bot's is captured; to see in game: a Huppermage casting an elemental spell then
  Runification on the same enemy, a Féca's Transhumance on its glyph, a Sram pushing an enemy into its trap.
- Step 9's mechanisms: the Roublard against big enemies with `Fights:Alone`, the Féca's glyphs, an Osamodas's summons and the
  fight map's preview, an aggression, stairs and portals, the inactivity popup, what the client shows once its turn's end goes
  unanswered (D367).

- The effects of D491: measured on the 217 captures of 18 to 21 November and replayed by breed, tested on the imagined
  fight only. To see in game: a character no longer surprised by a monster's reduction of 105 nor by a lowered resistance,
  a Zobal's Appui or a Forgelance's Charge Héroïque planned from the cell it lands on. Left, ranked in the audit: the dodge
  of AP and MP, the damage reflected (107), erosion, the Sacrieur's Souffrance, the caster moved through a portal.

## Next

0. **The studio made simpler** (D513, the owner's ask of 2026-11-24 for the next release, the choices delegated to the lead), four
   stages each a pull request: (1) the navigation and the shell, made (D513); (2) the home page and the character's card, made
   (D514); (3) profiles and routes, made (D515); (4) the settings and the statistics, made (D516). The four are done; to see
   in the app with the owner. The study
   is in the session's `artifacts/studies/ui-redesign.md` (git-ignored), its plan in D513.
0. **The studio's shell after next** (D540, the owner's « Oui, les 4 premières » of 2026-11-25, the choices delegated), four
   stages each a pull request: (1) the command palette, made (D540); (2) a status bar as VS Code's, made (D541); (3) the layout kept after a
   restart, made (D542); (4) empty states that guide, made (D543). The four are done. Each seen in headless drawings; to see in the app with the owner.
0. **Step 10, several accounts and groups, comes first** (D429, the owner's choice of 2026-11-18). 10a is done (D435):
   the party, a fight joined and the exchange between two characters mapped, format 18, and the party in
   `CharacterState`. 10b is made in code and tests (D437): the party by the bot, the "Follow" activity and the chain,
   to see in game. 10c is three pull requests (D440): its fights (10c1), the life before an attack (10c2, D441) and the
   group's bank (10c3, D442) are made in code and tests; next, 10b and 10c measured in game together, the owner's choice, and 10d, begun with the group's reply (D467: the enemies' target; next, the allies' spells and their turns in the reply, then the group's threat). The mapping 20 (D442) is published on the release `client-3.7.14.18`. Which of the follower's two automatic options is which waits on a short capture (D435).
   What follows waits behind it.
0. **Dungeons, step 12's other half** (D520 to D525, the owner's ask of 2026-11-25), four pull requests: (1) the pack's dungeon
   table, format 14, and entering, merged (#429); (2) the rooms' chain, each room's group fought and the server's move followed,
   the rooms left on foot, a lost fight, made in code and tests (D525); (3) the exit on the final map, the runs counted, the bank, the profile's form
   in the studio and the activity in the catalogue; (4) the follower entering behind its master (D521). The study is in the
   session's `artifacts/studies/dungeons.md` (git-ignored). The pack of format 14 is to publish on `client-3.7.14.18`.
0. **The AI playing the challenges** (D461, the owner's ask of 2026-11-19), three sessions: the challenges read, done in
   code and tests, mapping format 22; the challenges' table in the pack and the AI playing for the easy ones, done in code and
   tests, pack format 13 (D466); eleven more of the owner's list, the kills' order and the targets among them, done in code
   and tests (D497); left, choosing among proposals and the challenges no one asked for.
0. **The harvest going to another zone of its map** (D424 (b), D425) is made in code and tests: to look at in the client with the maps listed above.
0. **The refactoring and tidy-up session** (D399 to D401) is mostly done: `decisions.md` is cut in files by group of the index,
   `client.md` has its game connection apart, `AssetsViewModel`, `BotActor`, `HarvestActivity`, `WorldMapView` and `Imagination`
   are under 900 lines, BenchmarkDotNet is gone. The two files that stayed over 900 lines, `FightJudge` and `RouteEditorViewModel`, are cut too.
0. **Bug reports with the player's capture** (D409 to D411, D422, D423): BR1 and BR2 are made. The game connection is recorded by default
   with its retention and the notice (`Network:Captures:Game`, `CaptureRetention`); what names the account in a capture was measured
   ([client-game.md](client-game.md#the-game-what-names-the-account)) and the cleaner hides it by value (`CaptureScrubber`, D423); a report is
   written and read as a zip with a manifest (`ReportWriter`, `ReportReader`) and `scripts/report.cs` unpacks one. BR3 is made: Help, Report a
   bug writes the file with its code, and BR4 is made: a click sends it to a private Discord webhook (`DiscordReportSink`), read from the asset
   `webhook.txt` of the release `report-channel`, which `scripts/release.cs --report-webhook` publishes, and which is published.
   The journal is kept on disk and its last day joins a report (D504). What is left is to see it in the app.
1. **7r4, following a run, and 7r6, repeats and sequences as steps, are made in code** (D419, D421), to try in game with the owner's lists; then 7e.
   7r6 left: the condition read between two steps of a route, and, or and not in an "until" (7e's), a pass of a repeated one-way route on the card,
   the lists that repeat a route shown on the route. What 7r3 and 7r4 leave: the map's frame, a panel taller for the Sequencer, the steps already
   passed dimmed on the map, and recording in the toolbar, which waits for the measure in game.
2. **A measure in game with the owner**, before anything else of step 7: the list above, and what 7d4b will record.
3. **Session 7d4b**: recording while playing and Simplify (D299, D394), once the events it reads are measured.
4. **7e and 7e2**: the canvas of behaviors, then 7f to 7i (nodes and planning, debugging, the AI's fight preferences, sharing).
   The breeds, set aside since 9f5, come back once these are done (D306).
5. **Then** step 9's remaining mechanisms, and steps 10 to 13 (several accounts, marketplace and crafting, treasure hunts and
   dungeons, the owner's profession scripts).

The measure in game is worth a session of its own before 7e grows the code further.

## Open questions

Q5 (the fight formulas left: shields, AP and MP removed, erosion), Q12 (what quests, breed and the server's codes gate),
Q16 (casts and walks the server refuses that the rules allow), Q17 (a build that changes its schema). The table is at the end of
[the open questions](decisions/open-questions.md#open-questions).

## Known debt

- `D3B.Bot` is about 17,900 lines in 224 files and `D3B.Studio` about 6,300 in 141 (D393). The border is the compiler's, one way:
  `D3B.Studio` references `D3B.Bot`, which knows nothing of it. Left over: `D3B.Studio.Routes` reaches the harvest and the fight
  through `MapHarvest` and `MapFight`, now public, so a module of `D3B.Bot` cannot declare nodes (the node API is the studio's):
  a future module in a project of its own can. Nine members and types of `D3B.Bot` became public so that the studio could use them
  (`BotContext.Actor`, `BotActor.Time`, `Log`, `PauseAfterStep`, `SettingDefinition.TryRead`, `ActivitySettingsBuilder`'s
  constructor and `Build`, `MapHarvest`, `MapFight`): a narrower seam would be a task of its own.
- Files over 900 lines (D399): `Imagination` is again (976 lines after D485 to D491), to cut with the replay of the captured turns. `FightJudge` (890, from 1,365: its pure turn arithmetic is `JudgeTurns`, its damage tables and landing caches `JudgeTables`, its odds `JudgeOdds`; its score's terms, threat, approach, marks and buffs stay in it, cut only with the replay of the captured fights, `scripts/fights.cs --turns`, compared before and after) and `RouteEditorViewModel` (886, from 1,238: its texts, map scene, step rows, action forms, undo history, path planning, typed anchor, trace and world menu are cut). `AssetsViewModel` (885), `Imagination` (864, its pure fighter helpers are `ImaginedFighters`), `WorldMapView` (848, its hit test is `WorldMapHitTesting`), `BotActor` (723) is cut; `HarvestActivity` is again over (1,014 after D518 and D519), its rotation (`Draw`, `PlacesOf`, `RotateAsync`, `NextOf`) the next part to take out. `D3B.Bot` stays one project: its core (`Runtime`, `Activities`, `Banks`, `Movement`, `Journal`, `Characters`) refers to itself both ways, `Harvesting` and `Fighting` are the leaves.
- Undo has two histories: the route editor keeps its own snapshots (D375), the list of steps, the profile and the zone editors use
  `UndoHistory` (D394). Moving the route onto it is a change of no use by itself; do it when the route editor is next opened for another reason.
- The zones are read by the harvest and the hunt as a place (D415) and by their editor; a zone is not yet a filter of the Automatic place, and the nodes and conditions of 7f do not read them.
- `GroupJudge.OddsOf`, `Beats`, `Winnable` and `Share` are called by nothing in `src/` since D387, only by
  `scripts/fights/HuntBench.cs` and their tests. D387 kept them on purpose, for that bench: if the bench is dropped, they go
  with their tests; `Allows` and `Character` are alive.
- `docs/decisions/` is cut in files by group of the index, about 100 KB at most each (D401): read the index, then the file of the area worked on.
- The tests under load (2026-11-20): a test host that hangs, killed or timed out, leaves the summary's `failed` at 0 and lowers
  its `total`, the lost host counted only on its `error` line: read that line before trusting a count. CI now keeps a dump
  of a host that hangs five minutes or crashes (the `test-dumps` artifact). Fixed: a fight's pilot left waiting as its bot
  stopped held `Bots.DisposeAsync` forever; and, on 2026-11-21, a test moving the fake clock once just after it saw a step
  end, before the delay that followed set its timer, which then waited for a clock that never moved again:
  `BehaviorActivityTests.AListThatRepeatsReportsItsFirstStepAgainWhenItStartsOver` and eight more of its class, one of
  `FollowRouteTests`, two of `ActivityRunTests`. Eight of them then awaited the run's end or its next step with no deadline,
  which hangs the host rather than failing a test: the likely cause of `D3B.Studio.Tests`' 14-minute hang. And
  `HarvestViewsTests.AGivenProfileIsReadOnlyAndItsCopyIsTheProjectsOwnWithItsValues`, which hung three runs of six under
  load: its asynchronous block was dispatched as a value, so what it resumed after a save ending late went to an interface
  thread no longer running it. Not explained: `D3B.Studio.Tests` once ending with zero tests and exit code 1; its next
  occurrence leaves a dump. Fixed on 2026-11-22: `HarvestActivityTests.AHarvestWhoseWalkTheServerCutsByMovingTheCharacterLeavesItsMapGoneThrough`
  (D488), once red in a whole run, its map left `Probable`: the walk's refusal ended the use, and the harvest looked at the map
  before the bot's state applied the next map, sent just after it. The test now plays the measured order, the refusal and the
  next map answering the walk's confirmation, which ends the walk on its delay; the harvest also looks again at the next step,
  a test holding the next map until the step is over. And
  `GroupGateTests.AMasterGoesOnWithoutAFollowerMissingFiveMinutesAndWaitsForItAgainOnceItIsBack` (D490), which hung two
  runs of sixteen under load, and its neighbour `AFollowerLeftBehindIsWaitedForAgainOnceItLeftTheMapItWasLeftOn` the same
  way: the master's gate said whom it waited for before it set the delay it waits on, so that the test's jump of the clock
  could fall between the delay's measure and its timer, which then counted five minutes from the jump. The gate now sets
  its wait first; both tests await the master's action with a deadline. A wake of that wait between a test's two moves of
  the clock, which says nothing new, still leaves the same gap, a red test now rather than a hung host. Seen failing under load (ten test hosts at once) and not fixed:
  `GroupGateTests.AFollowerOnTheMapButOutOfThePartyIsWaitedForAndInvitedAgainEveryTenSeconds` (seven runs of twenty-one, its
  clock read some tenths late as the bot lags), two of `GroupFightTests` and
  `FrameRecorderTests.ABotsFramesAreRecordedFromItsConnectionsFirstFrameBothSidesAndD3BsOwn` once each; the group and
  fight tests move the clock once after a settle in about 25 places, not looked at.
