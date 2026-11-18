# Step 10, groups (D429 to D436)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D429: Step 10 starts now, on the C# version, in four sessions

*2026-11-18, step 10, chosen by the owner ("oui mais on le fait sur l'app C#"), the split the owner's among the lead's
three. Builds D23, D87, D298; moves step 10 ahead of what `status.md` listed before it.*

- **Now**: the owner asked for several characters fighting together, a master leading and its followers joining its
  fights with the AI. Step 7's measure in game, 7d4b and 7e, and step 9's remaining mechanisms wait behind it.
- **On this version**: the rewrite on `next` is another project and takes nothing of this step.
- **Four sessions**, one branch and one pull request each:
  - **10a**, the party's protocol read: the owner's captures of a party and of a fight joined (the list in
    [roadmap.md](../roadmap.md#10-several-accounts-and-groups)), the messages mapped and pinned, and the party in the
    character's state (its id, its leader, its members, an invitation received, a member's fight); no behavior.
  - **10b**, the party by the bot and the "Follow" activity (D430, D431), and the group moving in a chain (D432).
  - **10c**, the group's fights (D431, D434) and its bank (D433), and the group's life before an attack.
  - **10d**, measured in game on the owner's characters, the AI corrected from the group fights captured (D434).
- **Already there**: anyone waits on what another bot's state becomes (`BotActor.WaitUntilAsync`, D87); several clients
  run at once through one host (D23); the AI plays every fight a bot is in, so a follower in a fight is played (D198).
- **Out of scope, as D2 says**: no proxy per account, no humanlike delays between the members' actions; the references'
  random delays for invitations and acceptances are not taken.
- **Rejected.**
  - Designing now and coding after step 7's measure in game: the owner wants the step now.
  - Six sessions, the party, the chain, the fights and the bank each apart: more pull requests than the owner wants.
  - The fights first, the party made by hand: the owner chose the bot's party (D431).

## D430: A follower plays a "Follow" profile, which names its master

*2026-11-18, step 10, chosen by the owner among three options, as the lead recommended it. Builds D99, D298.*

- **A new activity**, "Follow" (« Suivre »), given to a follower as any profile is (D99): its setting is the master, a
  character of the accounts page. The master keeps its own profile, a harvest, a hunt or a list, and leads as soon as a
  connected bot follows it; a bot followed by nobody plays as before.
- **The order of the chain** (D432), the lead's choice: the followers' order in the master's party, then their names'.
- **Why**: nothing new in the database but a setting; the group changes what it does by the master's profile alone; the
  roles of D298 are the profile's.
- **Rejected.**
  - A group as a record of its own on the accounts page: a table, a migration and a page more, and a character of a
    group no longer choosing its profile.
  - A group as an asset of the studio: the most flexible for 7e and 7f, the heaviest, and the studio not yet measured in
    game.

## D431: The party is the bot's, the game's own following off, a fight joined by request

*2026-11-18, step 10, chosen by the owner, the bot's party as the lead recommended it, the game's following off the
owner's ask, the join by request as the lead recommended it.*

- **The party**: the master invites its followers by name once they are connected; a follower accepts only its master's
  invitation and refuses any other; both do it again after a new connection. A follower in another party leaves it
  first.
- **The game's options of a party member** stay off, and are turned off when found on: following the leader, joining its
  fights by itself, and ready by itself. The bot moves its followers (D432) and places them (D434); an option of the
  account would act when the bot does not play.
- **A fight joined**: when the server says a member of the party is in a fight, a follower on its map asks to join it
  (`FightJoinRequest` in the references, the master's id and the fight's), is placed by the AI and says it is ready.
- **The fight locked to the party**: the master no longer closes its fight (D216), which would keep its followers out,
  but locks it to the party; spectators still refused. The references name option 1 the party's, which 10a measures.
- **Seen in the references, for ideas** (D3): Bubble.D3.Bot's leader invites bots by name, its followers accept only
  bots' invitations and join on the party's fight event; MoonBot's API keeps the game's following off and its readiness
  off, so that the placement comes first.
- **Rejected.**
  - The party made by hand by the owner: done again at each disconnection or crash, nothing works without the owner.
  - No party in game: the fight could not be locked to the party, and stays open to strangers.
  - The game joining the follower to the fight by itself: an option of the account that may stay on when the bot does
    not play, and a behavior of the server to measure.

## D432: The group moves in a chain, and waits for anyone missing

*2026-11-18, step 10, chosen by the owner, "à la queue leu leu", the lead's reading of it confirmed; the waits as the lead
recommended them. Builds D87.*

- **The master plays its profile**; after each map change it waits for every follower on its map before it acts again:
  an attack, a harvest, a zaap, the haven bag, a door.
- **In a chain**: each follower leaves by the exit the member before it took, the first follower behind the master, the
  second behind the first; once on the map of the one before it, it waits there. A follower far from the chain, after a
  lost fight or a new connection, goes to the master's map by its own world paths, then joins the chain.
- **Someone missing**: a member disconnected, paused because the player took it over, or stuck, and the whole group waits
  where it stands, with no delay, as D87 wants; the bot's card says whom it waits for. The member back joins the chain
  and the group goes on. A master missing is waited for too: no follower leads in its place.
- **Rejected.**
  - Waiting for the followers only before an attack: faster, but a follower falls far behind, through zones and zaaps it
    does not know, and the group starts again at every fight.
  - Each follower by its own shortest path to the master's map: what the owner did not want, the members scattered on
    the way.
  - Going on without a missing member after a delay: fights with fewer members than planned, and a delay to set.
  - A follower leading when the master is missing: the master's profile would have to move to another character.

## D433: A full member takes the whole group to the master's bank

*2026-11-18, step 10, chosen by the owner ("si un membre est full pods faut que tout le monde aille à la banque +
échange sur master et master pose en banque"), what is given as the lead recommended it. Builds D405.*

- **One member full**, master or follower, and the whole group goes to the master's bank, in its chain.
- **There**, each follower exchanges with the master what the general banking settings (D405) would deposit, never its
  kamas; the master deposits it, between two exchanges when its pods do not hold a follower's whole share; then the
  master deposits its own and the group goes back to the master's profile.
- **Why**: the items of the group in one account's bank; one rule for every member, nothing new to set.
- **The life before an attack**, the lead's choice, local and reversible: the master attacks only once every member's
  life is over the share its profile asks, each member healing as its own fighting activity does.
- **Needs**: the exchange between two players, which no capture holds yet, is captured in 10a.
- **Rejected.**
  - Each member at its own bank: what the owner did not want.
  - The kamas given too: a follower without kamas no longer pays a zaap.
  - What is given as a setting of the "Follow" profile: one setting more, no need for it yet.

## D434: The AI unchanged in a group, then measured

*2026-11-18, step 10, chosen by the owner, as the lead recommended it. Builds D244, D249.*

- **The AI already counts its allies by team**: their life and shields in the judge's worth, the threat on the ally most
  exposed, heals and shields on allies. Each bot plays its own turns with it, as now.
- **What changes in 10c**: the master locks its fight to the party (D431) and says it is ready only once every follower
  of its map is in the fight and placed; a follower says it is ready once placed.
- **10d** captures the group's fights on the owner's characters and corrects what they show: a target shared, the
  placement beside the allies, the heals between characters.
- **Seen in the references**: Bubble.D3.Bot scores heals and buffs on allies and has no shared target; MoonBot's
  followers may play their master's fight script.
- **Rejected.**
  - A shared target from the first version: a change of the judge no fight measured, which the AI's killing first already
    does in part (D203).

## D435: 10a, the party's protocol read from the owner's captures, mapping format 18

*2026-11-18, step 10, session 10a, measured on the owner's captures of that day; the two options' neutral names the
owner's choice ("plus tard") to a capture that would tell them apart. Builds D429, D431, D216.*

- **The captures**: three game connections of 3.7.14.18 through the app, the leader's of 5 min 53 s and its follower's of
  5 min 3 s, two of the owner's characters on one account each, and a third of 44 s; described with `scripts/capture.cs`,
  each value compared on the machine with a tool of the session, never written down
  ([client-game.md](../client-game.md#the-game-in-a-party)).
- **The party, measured**: the leader's invitation names its guest by name; the server answers the leader with a party
  of its own at once, a new one at each invitation of a leader without one, four in the captures; the guest is told of
  the invitation, accepts or refuses it by the party's id, and once it accepts it has the party, the leader its new
  member. An exclusion and a leaving tell the others which member left, with its kicker or without, and a party left with
  its leader alone ends.
- **The game's two options of a member**, joining the leader's fights and ready by itself, are on by default: the client
  turns both on as its character joins a party, one after the other, each answered by an empty response, the party told.
  Which is which is not measured: they are mapped as the first and the second, the requests turning them off only, which
  10b sends both of (D431). Following the leader asks level 10 and a subscription.
- **A fight joined**: as the leader attacks, its follower on the map is told that a member of its party is in a fight,
  the fight's id, the member and the map, in the millisecond the fight is shown there; it joins by the fight's id and the
  leader's, as the references have it, and the server answers as to an attack.
- **The party's lock**: the server sets the fight's option 1 by itself as a party's leader attacks; the owner then closed
  the fight, option 2, and refused spectators, option 0, as D216 measured them. So 10c's leader leaves option 2 alone,
  which would keep its followers out (D431).
- **The exchange between two characters**: asked by one, told to both, accepted by the other, opened for both; the
  objects go in by the bank's requests (D80), each says it is ready, and the exchange concludes once both are, its
  objects moved and the exchange left in the same millisecond. Its type is 1.
- **Made**: `parties.proto` (17 messages and 2 nested, 2 enums and 2 nested), `FightJoinRequest`, six messages of the exchange, option 1 and the
  exchange's type 1 named; 22 pairs pinned for 3.7.14.18; mapping format 18, 251 messages paired, no failure, the same
  bytes twice, and pairing with the pins and the structure alone without the captures. `CharacterState` keeps the party,
  an invitation not answered, and the last fight a member is in (`CharacterParty`, `PartyInvitation`,
  `PartyMemberFight`, D9).
- **Left to 10b**: the state of a member's automatic options, which no event read here gives, so 10b sends both requests
  turning them off without knowing it; following the leader, which the follower captured could not turn on, neither
  measured nor mapped: a follower of level 10 and subscribed that has it on is not yet turned off (D431).
- **Left to 10c**: the fight shown on a map and its teams, which a follower arriving late reads; the end of a member's
  fight, which no message measured says; the exchange in the state, which neither a dialog nor busy covers yet.
- **Needs**: the mapping of format 18 published with the release `client-3.7.14.18` before an app that reads it, on the
  owner's go (D24).
- **Rejected.**
  - Naming the two options from the references' order: nothing measured says the client's order is theirs.
  - Mapping every party message the captures carry (the guests, the members' positions and lives, the refusal told to the
    leader): no step of 10b or 10c reads them yet.

## D436: 10b, the party by the bot, the "Follow" activity, and the group moving in a chain

*2026-11-18, step 10, session 10b; the follower's haven bag and the master waiting for its party the owner's choices among
two each, as the lead recommended them; the rest the lead's, local and reversible. Builds D87, D99, D430, D431, D432, D435.*

- **Measured** on the follower's capture of 10a (`scripts/capture.cs --messages`, structure and times only): as its
  character joins a party, the client sends the two requests turning the automatic options on 3 to 22 ms after the
  party's event, each answered 27 ms later, as long as its own window has them on; after the owner turned them off in
  the window, the next party joined brought no such request. D3B's requests do not go through that window, which the
  lead reads as turning them on again at the next party (not measured): the follower turns both off at each party it
  joins, once the client's display time passed (D151).
- **The master's gate** (`GroupGate`, `IActionGate`): every request a behavior sends goes through
  `BotContext.SendAsync`, which holds those that start an action out of a fight, a walk, a map change, a use, an NPC
  talked to, a zaap, the haven bag and an attack, while a bot follows the bot sending them. So any profile leads, a
  harvest, a hunt or a list of routes, with no activity changed, and the master waits after each map change and, a
  member missing, where it stands. It waits with no delay (D87) until each follower is connected, not paused, out of a
  fight, placed on its map and in its party; the card says whom for (`BotActor.WaitingFor`, `GroupTexts.Waiting`).
- **The party**, as the gate waits: the master leaves a party another leads, again every 10 s while it is still in it,
  then invites by name each follower
  connected and out of its party, again once the follower's connection is a new one, and every 10 s while unanswered,
  the owner's choice. The follower ("Follow", `FollowActivity`) leaves a party its master does not lead, accepts its
  master's invitation and refuses any other once (D3B's own refusal is no message its bot sees, D62), then turns the
  options off.
- **The chain** (`GroupChain`): the followers in the master's party's order, then by name (D430). A follower with its
  master waits for it to move; behind the one before it on a map, it waits for that one to leave; else it travels to the
  master's map by the world's paths: from the map the master left, the way between the two maps, from afar its own
  journey (D74); a journey or a haven bag that fails, it says it is stuck and tries again once its master moved, or
  after 5 s. As the master enters its haven bag, each follower enters its own, and leaves it as the master leaves
  his: together means on one map or each in its own bag, the owner's choice.
- **The groups** (`GroupDirectory`, `Bots.Groups`): a follower is in its master's group while its activity runs, at most
  seven, a party holding eight; no follower leads, so a follower cannot be followed nor a master follow another, which
  fails the activity with its reason.
- **The master's setting**: a new kind, `SettingKind.Character`, a character of the accounts page by
  `CharacterKey.ToId` ("account:character"), picked in the route's dialog among the characters D3B knows
  (`CharacterFieldViewModel`); one D3B no longer knows blocks the profile.
- **A race found by the tests**: a wait begun after a decision missed a follower leaving between the two, and the
  master waited for ever; each decision now reads the bots and the groups' next change first (`BotSeen`).
- **Left as D435 left it**: following the leader, the third option, neither measured nor mapped: a follower of level 10
  and subscribed whose client has it on is not turned off, and the game may move it beside the bot (D431). It waits on a
  capture of such a follower turning it off, owed before 10d.
- **Not measured in game**: the whole of it; 10d, then the owner's two characters, each in its own client through the app.
- **Rejected.**
  - The wait in each activity: every activity, and the routes' actions, changed for one rule that holds of all.
  - Replaying the exact transition the master took: the bot does not see D3B's own requests (D62), and the way the graph
    gives between two neighbouring maps is that transition, a cell of the same exit not always the master's.
  - Entering the master's own haven bag: not measured.
  - The master waiting only for its followers on its map, the party invited and not waited for: a follower out of the
    party would only be found missing at the fight, locked to the party (10c).
