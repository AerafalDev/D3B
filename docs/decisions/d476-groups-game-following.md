# Groups, the game's own following for a subscribed group (D476)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The game's options
turned off are [D431](d429-d435-step-10-groups.md#d431-the-party-is-the-bots-the-games-own-following-off-a-fight-joined-by-request);
the walks copied, [D447](d447-groups-following-in-real-time.md).

## D476: A group the game can follow turns its options on, and the bot watches each follower

*2026-11-20, step 10, the owner's ask, the hybrid the lead recommended among three, with the owner's addition: watch that
the options stay on and turn them on again. Supersedes D431 for such a group. Builds D72, D435, D447, D456, D471, D475.*

- **Asked**: when every account is subscribed and the characters are past level 10, the followers follow the master, join
  its fights and are ready by the game's own options; else the app drives everything, as before; and even so, the bot
  watches that no character is stuck. The owner then added: when an option goes off, the follower goes to its master's
  map and turns it on again; and reported that the leader's "follow me" turned off stops its members, turned on again
  makes those whose option is on follow again.
- **Measured** on the owner's game of 2026-11-20, 05:53 UTC, four connections of 3.7.14.18 through the host, played by
  hand (`scripts/capture.cs --messages --frames`, structure only):
  - **Following is the client's**: a member asks to follow its leader, the server answers, then tells it at each of its
    leader's walks where the leader is, and its client sends its own walk 4 to 6 ms later. Its other members are told
    that it follows, or no longer follows, by its id.
  - **The leader's "follow me"**: the server passes it to each member, whose client asks by itself 3 to 5 ms later to
    follow, or to stop following.
  - **Joining and ready are the server's**: the followers were in the leader's fight in the millisecond of its attack,
    with no request and no word of a member's fight, and the four ready in the millisecond the leader was, with no
    request of the followers.
  - **The options on**: the two requests the client sends at each party joined (D437) turn the two options on; the owner
    ticking them on in the captures of 2026-11-18 sent the same.
  - **Not measured**: a map changed while following (the leader walked on one map only), a door, a zaap, a member whose
    own option is off, a refused request to follow.
- **The group the game follows** (`GameFollowing`): every account of the group subscribed (D72), every follower of
  level 10 or more, which the game's following asks (D435), and the master leading its party, since the game follows the
  party's leader (D471). Else the bot plays as before.
- **Made**, mapping **format 24**: the two options' "on" requests, the leader's "follow me", the member's requests to
  follow and to stop with their answers, and the word that a member follows or stopped. The state knows whether the
  character follows its leader (`CharacterParty.FollowsLeader`), from those answers and words.
  - A follower turns the two options on at each party, instead of off, and off again with its following when the group
    no longer qualifies.
  - On its master's map and not following, it asks to follow, at most every 10 s; the master, before its requests, asks
    its members to follow it while one with it does not, at most every 30 s.
  - Following, it copies no walk of its master's: its client walks after it, and two walks would race (D456); not
    following, such as refused, it copies them as before. A master connecting again, its party not given yet, changes
    no follower's options.
  - Off its master's map, following, it waits 10 s after its master arrived on its new map for the game to bring it,
    then goes by its own paths, as before; its haven bag, the exchange of the bank and its life stay the bot's.
  - Its master's fight told and not joined within 1 s, it joins by request, as before, and turns its options on again.
  - The AI still places it, and the master still says it is ready once its followers are placed (D440).
- **Rejected**:
  - The game's options for the fights only, the bot walking: the owner wants the game's following itself.
  - Measuring first, a run with the following across maps, a door and a zaap: the owner judged the captures enough; the
    10 s fallback covers what the game would not do.
  - The master's "follow me" alone: a member whose option is on follows by its own request, which needs no call.
