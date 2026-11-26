# Dungeons, the master's spare keys given to its followers (D569)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The dungeons in a
group are [D528 and D529](d520-d523-dungeons.md#d528-in-a-group-each-follower-enters-and-leaves-a-dungeon-by-itself-and-the-group-leaves-out-a-dungeon-a-follower-cannot-enter);
the exchange between two characters, [D442](d442-step-10-the-groups-bank.md) and [D482](d482-group-exchange-one-request.md).

## D569: At a dungeon's door the master gives each follower lacking a key one of its own by an exchange, keeping what its own entry needs

*2026-11-26, roadmap step 12, the owner's ask (a follower without a key given one by a master that has more), the rule the
owner's choice between the two the session put, as the session recommended it; the exchange's shape the session's within
D442. Refines D528 and D529. Builds D442, D458, D482, D523, D524.*

- **Read in the code**: each member kept its keys and keyring (D529) and a dungeon one follower carried no key for was left
  out of the group's turn (D528), whatever the master carried: a master with ten keys and a mule with none ran nothing.
- **Made, the owner's choice**: before each entry the master keeps what its own entry needs, one key, none when it carries a
  keyring that opens the dungeon, and gives each follower lacking a key what it lacks (`DungeonKeys`). Each run reckons again,
  so that the group's keys are shared run by run, and the group runs as many times as they allow together. The master's turn
  leaves a dungeon out only when its spare keys do not cover every follower lacking one, in the chain's order, saying so.
- **Made, the exchange** (`GroupHand`, `GroupDirectory.Hand`, `FollowActivity`): at the door, the group gathered, one follower
  an exchange, before the keeper is talked to. The follower asks the exchange as at the bank (D442), once a round; the master
  accepts after the client's display time, moves the key in by `ExchangeObjectMoveRequest`, the only request naming a quantity
  (D482), says it is ready 3.5 s after the last change (D458), and the follower says it is ready after it, which concludes.
  Then each enters by itself (D528). An exchange that does not conclude, the master waiting 30 s at each wait as at the bank,
  is tried again; three in a row shut the dungeon for the run (D524), lest the group wait at its door for ever.
- **Not measured, tested on the fake game only**: a master moving objects into an exchange its follower asked, which the
  protocol lets either side do; whether the server takes a key given that way, a key being no bound object in the tables.
  To see in game: a mule with no key entering behind its master, the master one key fewer.
- **Rejected.**
  - The master keeping the keys its profile's planned runs need, giving only beyond: in a group the master never enters without
    its followers (D528), so those keys open nothing, and a profile of runs without end would never give one.
  - The keyring given: it is not a key spent, and the master would then lack it (the owner's ask left it out).
  - The master asking the exchange: the follower's asking is the one measured (D442), and the follower's code keeps one way in.
  - The keys given anywhere before the journey: an exchange needs both on one map, which the door's gathering already makes.

## D570: The app's release after 1.1.0 is 1.1.1, holding the profiles that restart and the master's spare keys given

*2026-11-26, the group keys session, the owner's « push / pr / merge / publish / release » once the keys were done, then « 1.1.1 »
among 1.2.0, the session's recommendation, and 1.1.1, and both branches in it rather than the keys alone. Builds D566, D567,
D568, D569.*

- **What it holds.** The profiles restarted after a failure, the behavior speaking for its activity in every way (D567), the
  deposits leaving out a quest's objects and what weighs nothing (D568), and the master's spare keys given to its followers at a
  dungeon's door (D569): two pull requests merged into `main`, the first before the second.
- **Published first.** Nothing: neither the pack nor the mapping changes format, and `client-3.7.14.18` holds format 15 and 26.
- **Tagged.** `v1.1.1` on `main` once both pull requests are merged and its CI is green.
- **Rejected.** 1.2.0, the session's recommendation for a new behavior: the owner reads the release as fixes for the groups and
  the profiles. The keys alone: the profiles' fix is what lets a follower of a studio profile follow its master in at all.
