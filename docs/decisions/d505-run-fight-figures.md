# A fighting profile's numbers (D505)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The runs kept for
the statistics [D146](d126-d175-step-6.md#d146-harvest-statistics-in-d3bs-database-and-a-page-of-their-own); a fight that
cuts a harvest [D144](d126-d175-step-6.md#d144-a-fight-while-harvesting-resumed-paused-or-stopped-as-the-profile-says);
the deaths `scripts/fights.cs --live` counts [D463](d463-fights-by-breed.md#d463-scriptsfightscs---turns-and---live-print-their-main-counts-by-the-characters-breed).

## D505: A run counts its character's fights won and lost, its deaths, and the experience and kamas they gave

*2026-11-24, U3 of the next release's scope, which the owner validated that day; the choices below are the lead's, local
and reversible but for the run's record. Builds D146, D144, D463.*

- **What was found.** The card counted the fights that cut a harvest (D144), and its kamas were the purse's change from a
  start the app kept in memory: a bank's deposit of kamas showed as kamas spent. Each fight's experience and kamas were read
  (`FightEnd`) but only written to the journal (`FightEndEntry`), and the run's record kept items, professions, banks and the
  fights that cut it: a hunt profile could not be judged, and nothing of its fights outlived the app.
- **What was made.** The run counts every fight that ends while it goes on (`ActivityRun.Fights`, a `RunFights`): won, lost,
  the character's deaths, the experience and the kamas they gave. The bot's loop counts it where it writes the journal's
  `FightEndEntry`, so the two always agree. A death is the character's fighter dead as the fight ended, the rule `--live`
  counts by (D463): a fight the team won may cost one. The player's own fights during the run count too, since they are the
  character's numbers. `RunRecord` keeps the five figures (migration `FightFigures`, 0 for the runs before it).
- **On the card**, once the run fought, a block gives the experience and the kamas with their rate an hour, and the facts
  read the fights, won and lost, deaths and deposits; the purse's change is gone. **In the Statistics**, a block
  "Fights" sums the runs counted, won, lost and deaths beside its title, and gives the experience and kamas by hour of the
  runs that fought alone: a harvest's hours beside a hunt's would hide what the hunt gives.
- **Rejected.** The character's experience and kamas from its state: the state holds no character experience, and the
  purse moves with banks, zaaps and sales, not only fights. The figures read back from the journal: it is bounded, and the
  journal's own work may change it. The fight's `alive` flag of its result for the deaths: not measured, where the fighter's
  death through the fight's actions is (D463). The outcomes neither won nor lost (values 1, 3 and 4 of `FightOutcome`, never
  captured): their experience and kamas count, the fight itself in neither column.
