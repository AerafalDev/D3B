# The harvest, fights lost in a row (D480)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). A fight while
harvesting is [D144](d126-d175-step-6.md); a fight lost and the three in a row, [D198](d176-d208-step-9.md).

## D480: Fights lost in a row no longer end the harvest, and a loss is followed by the whole life regained

*2026-11-21, the owner's report from game: "the Pandawa levelling its farmer was disconnected several times, find the
cause"; the lead added that the Pandawa's AI, not its level, loses these fights (a session of its own). The owner chose
each part among the options the session put, one at a time. Supersedes D198 in part (three lost in a row stop the harvest;
the life regained to the share it heals under).*

- **Measured** on the owner's captures of 2026-11-21 (08:03, 09:56 and 12:32 UTC; `scripts/capture.cs --messages`,
  `scripts/fights.cs --live`, and a tool never committed that prints levels, life and delays): the 14 fights of a Pandawa
  of level 21 harvesting cereals were all a resource's protector, the server starting each 1 ms after the character's own
  harvest ended, never an aggression. The enemy was always one monster of level 30, a scarecrow of wheat or of barley;
  11 were lost. Each began at full life, 209.
- **The kick**: at the third loss in a row the harvest stopped (D198); D3B sent nothing more, the client only its
  heartbeats, and the server closed the connection 30 minutes after the fight's end, to the second, three times.
- **The life after a loss**: the server left 105 of 209, half rounded up, which is never under the default share of 50
  the regaining waits for, so the character walked on 1.3 to 1.5 s after each loss. That cost no fight here: the life
  comes back a point each half second, and the next protector came 59 s or more later, at full life.
- **Made**: a fight lost is answered as the profile's "After a lost fight" says, however many came in a row; the harvest
  never stops for losses. The hunt keeps its three (D198, D267). After a loss the character regains its whole life before
  going on, by its food when the profile heals (D148), else waiting a point each interval (`HarvestUpkeep.RegainAsync`,
  D153): 52 s at 209 life. Out of a loss, the share it heals under is unchanged.
- **An activity that ends for good** (a failure: no map reachable, nothing to harvest, still full after the bank) leaves the
  connection idle until the server closes it: the owner's choice, the card and the journal already saying why.
- **Left**: nothing in the bot reads the character's energy, which each loss costs; a long run of losses would make it a
  ghost, the harvest walking on regardless. A guard at low energy is the owner's to ask for.
- **Rejected.**
  - Leaving out, until the next level, the resource whose protector won, or after two such losses: proposed first; the
    owner wants the harvest to go on as before, energy lost to the defeats included.
  - Keeping the profile's share after a loss: no fight of the captures began below full life, but a protector met at
    once would be fought at half.
  - Keeping the connection alive once an activity stopped: feigning a player, out of scope (D2, D366).
  - Closing the connection at once, or going to the haven bag, once an activity failed: the first unmeasured, the client
    showing a lost connection and perhaps reconnecting; the second still closed after 30 minutes.
