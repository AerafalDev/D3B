# Step 10, groups (D429 to D434)

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
