# Groups, the master emptied at the bank (D452)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The group's bank is
in [D442](d442-step-10-the-groups-bank.md).

## D452: At the group's bank the master deposits all it does not wear first, and again when an exchange does not conclude

*2026-11-19, the owner's retest in game of main 8291c6a7 ("le maître est full pods mais lance quand même l'échange avec
les mules … normalement le maître devrait savoir qu'il est full pods et se vider en banque avant"; "prendre en compte tout
l'inventaire et pas juste ce qu'on a récolté"); all it does not wear but its food the owner's choice between two, as the
lead recommended it. Supersedes D442's master depositing by its profile's rule. Builds D441, D442.*

- **Measured** on the owner's retest (four game connections of 3.7.14.18 through the app from 01:25, a master and three
  followers; the app's log; `scripts/capture.cs --messages` and a throwaway reading on the machine printing counters and
  message names only): the master, 4,128 of 5,117 pods, went to the bank with its group and took its followers' objects
  before depositing its own. Of three exchanges, the followers moved 50, 43 and 60 stacks in, one refused each time with
  an error text; each said it was ready naming as many changes as the server had told, as D442 reckons the step. The
  server told the master the readiness of one follower only: the two others' went untold, the master waited 30 s and left
  the exchange. Then it deposited only its loot and what it was given, the objects the owner had put in its bags by hand
  stayed, and its profile ended "still full".
- **Made** (`GroupBank`):
  - Once at the banker, the master deposits first, in one opening of the bank.
  - When an exchange does not conclude, the master deposits, once, and takes the rest.
  - The master deposits all it does not wear but the food that only heals, which it eats (D441), whatever its profile
    banks: its pods go down whatever it carries.
- **Not measured**: why the server told the readiness of one follower and not the others, the lead's reading being the
  master's pods; the owner looks at it again in game.
- **Rejected.**
  - The master depositing by its profile's rule, as D442 had it: the objects put in by hand kept it full.
