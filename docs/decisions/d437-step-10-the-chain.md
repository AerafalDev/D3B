# Step 10, 10b: the party by the bot and the chain (D437)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The step's design
is in [D429 to D435](d429-d435-step-10-groups.md).

## D437: 10b, the party by the bot, the "Follow" activity, and the group moving in a chain

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
- **Measured in game**, the owner's characters through the app: the master invited its follower by name, the follower
  accepted and turned the options off, and the master acted once its follower was in its party. The client's window for
  the invitation stays open on the follower's screen whether D3B accepts 0.51 s or 1.31 s after the invitation: the
  client closes it on the player's click alone, the server sending nothing that closes it (the bank's windows, D132, the
  server closes). A known limit, closed by hand; a message closing it would be D3B's first to the client, to study.
- **Measured in game, the chain**: twice the follower went round, 7 map changes where its master made 1, though the
  graph holds the one scroll between the two maps with no criterion. Its first walk after the master left, sent as soon
  as the server named the master's new map, was not followed by its client, the change failed, and the journey left
  that way out and found another (D74); its client confirmed 23 of its 33 walks, the master's all 21 of its own. The follower now leaves once
  its master is placed on its new map, and changes maps one a step, a change failing tried again by the same way, a
  whole journey only after 3 in a row. Why its client did not follow those walks is the lead's reading, not measured:
  its client still showed the master on the way out.
- **The answer's delay**, the owner's choice once measured: the follower answers an invitation no sooner than 1.3 s
  after it, the owner's own fastest in 10a's captures (1.28 s), and the client's display time at least.
- **A race found by the tests**: a wait begun after a decision missed a follower leaving between the two, and the
  master waited for ever; each decision now reads the bots and the groups' next change first (`BotSeen`).
- **Left as D435 left it**: following the leader, the third option, neither measured nor mapped: a follower of level 10
  and subscribed whose client has it on is not turned off, and the game may move it beside the bot (D431). It waits on a
  capture of such a follower turning it off, owed before 10d.
- **Seen in game** on the owner's two characters, each in its own client through the app: the party, the master waiting
  on each map, the chain, a follower's client closed and opened again; the owner found the rest good. Fights are 10c's.
- **Rejected.**
  - The wait in each activity: every activity, and the routes' actions, changed for one rule that holds of all.
  - Replaying the exact transition the master took: the bot does not see D3B's own requests (D62), and the way the graph
    gives between two neighbouring maps is that transition, a cell of the same exit not always the master's.
  - Entering the master's own haven bag: not measured.
  - The master waiting only for its followers on its map, the party invited and not waited for: a follower out of the
    party would only be found missing at the fight, locked to the party (10c).
