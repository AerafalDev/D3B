# Groups, the party kept and the fight's lock (D471)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The party by the
bot is in [D437](d437-step-10-the-chain.md), the group's fights in [D440](d440-step-10-the-groups-fights.md) and
[D448](d448-groups-followers-into-the-fight.md), the invitation at the first request in
[D457](d457-groups-invite-and-bank-refusal.md).

## D471: A party holding the master and its followers is kept whoever leads it, a master with followers never closes its fight, and opens one the server kept closed

*2026-11-20, the owner's report from the game of that night, a Huppermage master and three followers (Forgelance, Pandawa,
Zobal): the four already in one party formed by hand, the Forgelance leading it, each follower left it as its activity
started; the fight was locked before the followers joined, and the owner had to unlock it by hand. The owner's asks: keep
a party already formed with the master, only the party's automatic options turned off, and never lock a fight the
followers are to join. Measured by the lead and this session; the fix this session's, local and reversible. Refines D437,
D440, D457. Builds D216, D435.*

- **Measured** on the four game connections of 00:32 to 00:38 UTC (`scripts/capture.cs --messages`; the options, their
  states and the party's leader decoded in a scratch tool, nothing written down; [client-game.md](../client-game.md#the-game-in-a-party)):
  - Each follower left the party as its activity started, about 2 s apart, since its master did not lead it (D437); the
    master's party ended with the third. The master invited them again 1 min 49 s later.
  - Meanwhile the master attacked alone (00:50:06): out of a party, its fight was its own (D440), and the AI closed it
    and refused spectators (D216).
  - The server keeps a team's options from one fight to the next: the master's next fight, back in its party, began with
    option 2 (closed) and option 0 (no spectators) on, before any request. The three followers' joins, 0.45 s after
    their party's word, were refused by an event not mapped, and never sent again (D440); the owner turned option 2 off
    by hand. In the master's seven fights before 00:50, options 0 and 2 were off.
  - The server locks to its party the fight of a member that is not its leader: the master attacked by hand at 00:10:22,
    the Forgelance leading, and option 1 came on 30 ms after, as for a leader's fight (D435).
  - **Not explained**: from 00:49:05 to 00:50:50 the master's gate held no follower (no invitation at its walks, its
    attack not held, D457), and the followers sent nothing: their activities were not in the host's groups then. The
    captures do not say why (stopped by hand, or a failure); the app's log would.
- **Made**:
  - The master's party is the one it is in, whoever leads it (`GroupChain`): a follower is in it while it is a member,
    the chain's order is that party's, and a party another leads is kept while it holds each follower a connection plays
    (`GroupChain.HoldsAll`, `GroupGate`), the master turning its own automatic options off there, once a party and a
    connection, lest it join its leader's fights, a follower's (the review's finding); else the master leaves it and forms
    its own, an invitation into a party another leads not being measured.
  - A follower leaves only a party its master is not in, and turns the options off in one it is (`FollowActivity`); the
    master's wait for its placement counts a follower of its party out of a fight, whoever leads it (`GroupFight.Placing`).
  - A master with followers, or whose fight the server locked to its party, leads its fight (`GroupFight.Role`): it
    closes none, so that no lock is kept into the fight its followers join, refuses spectators, and opens a fight the
    server kept closed (`FightPlay`, each option toggled only when the fight shows it otherwise than wanted, read again
    after the player's pace, since the server shows the options after the placement's cells). A fight of a master with
    followers out of its party is open to strangers, though no follower is told of it. A follower's own fight and one of a
    character out of a party nobody follows are still closed as one alone (D216).
  - A follower joins its master's fight only once that fight is not closed, so that the master's opening comes first
    (`FollowActivity`, `BotSeen.Moved` seeing the fight closed or opened).
- **Not measured**: the owner looks at it in game: a party formed by hand kept, the followers' options turned off, no fight
  locked before they join, and one the server kept closed opened by the master. A master whose followers' activities do
  not run fights as one alone, open if in a party.
- **Rejected.**
  - The follower leading the party passing the lead to the master: the message is not mapped, and no capture holds it.
  - The master inviting a missing follower into a party another leads: not measured; it forms its own, as before.
  - Closing the fight later, once the followers are in: they are in the party the server locks it to, and strangers are
    kept out by that lock already.
  - A follower sending its join again after a refusal: the refusal is not mapped, and waiting for the fight to open
    avoids it.
