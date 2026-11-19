# Groups, joining from where they stand (D449)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Following in real
time is in [D447](d447-groups-following-in-real-time.md) and [D448](d448-groups-followers-into-the-fight.md).

## D449: A follower does not copy its master's walk to a group of monsters, and no two followers walk to one cell

*2026-11-19, the owner's ask after the retest of main c6cc5e25 ("les personnages ont pas besoin d'aller à côté de la
cell du meneur pour rejoindre le combat tant qu'ils sont sur la même map c'est bon"), then the owner's report of one
follower of three in the fight; the lead's fix. Refines D447, D448.*

- **Seen** on the owner's retest (the app's log): as their master walked to the group of monsters it attacked, each
  follower walked beside it first (D448), and asked to join the fight only once that walk was over. In the last fight,
  the three followers walked to one cell at once, the only free one beside their master's on the map's edge: the first
  joined, the two others' walks went unconfirmed, the server held them, and they asked to join too late.
- **Made** (`FollowActivity`):
  - A master's walk ending on the cell of a group of monsters its map shows is its attack: the followers copy it not, and
    join the fight from where they stand as the server tells their party of it (D435).
  - A follower walks to a free cell around its master's, then around those, none another member stands on or walks to,
    each follower its own by its place in the party, so that followers choosing at once choose apart.
- **Seen too**: in that last fight the master's client said it was ready once, 20 s into the placement while the bot
  waited for its followers, never in any other fight; the lead's reading is the owner's own click, left as the player's
  hand (D202).
- **Not measured**: the owner looks at the joins again in game.
- **Rejected.**
  - Cutting a copied walk short as the fight is told: the walk is the client's, which a request does not stop, and the
    copy is not needed at all.
