# Fights, the AI measured breed by breed (D463)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The replays of
the turns and the fights are [D206](d176-d208-step-9.md#d206-the-turns-and-the-fights-replayed-scriptsfightscs---turns-and---live);
the AI in a group is [D454](d454-group-fights-bolder.md) and [D460](d460-group-fights-strike-now.md).

## D463: `scripts/fights.cs --turns` and `--live` print their main counts by the character's breed

*2026-11-19, the lead's study of the AI by breed, whose copy of the bench, keyed by breed by hand, found the Forgelance's
spear throws left out by D408 and the reply overestimating the enemies in a group; the lead's choice, a guard for the
sessions of the AI to come (10d, the mechanisms shared by several breeds). Builds D206, D212, D407, D460.*

- **Why**: one AI for every breed (D244) is changed through the effects, and a change helps one breed while it costs
  another. The totals hid both of the study's findings: the spear throws planned 2 times against 10 played were lost
  among 1,619 searches (D462 has since given them back), and the reply's excess on the character among the summed
  blows on the whole team.
- **`--turns`** keeps every line it printed and adds, at its end, the reply's blows against the enemies' blows between
  turns, alone and in a group, for every breed (in a group: another character of the team alive as the turn begins, as
  the bot weighs a group's fight, D454), then a
  section by breed: the turns replayed, the plans (casts, walks only, nothing), the plans dealing more, as much or less
  than the turn played, the damage, kills and heals foreseen, the life the reply takes, the damage and kills over two
  turns and the judgement, planned against played, the damage the server announced, the reply's blows against the
  enemies' on the character, another ally and a summon, alone and in a group, and the spells planned against played by id.
- **`--live`** keeps every line and adds the character's deaths to the AI's fights, then a section by breed: the fights
  and how they ended, the turns and rounds a fight won takes, the character's own turns (not those of a summon or a
  companion it controls) and those with no cast nor walk, the
  life lost a fight and the deaths. A fight's breed is the character's at its first turn, else its selection's: a group's
  fight often ends before a follower plays (1,629 of the 2,969 fights had no breed before).
- **Measured** on the machine's 83 captures (client 3.7.14.18): every total identical to main dcbe44d1's, `--turns` and
  `--live` alike. The reply, alone on 10 turns, foresaw 247 on the character for 271 dealt (x0.9); in a group, on 1,171
  turns, 10,574 for 2,360 (x4.5), 22,942 on the other allies for 13,395 (x1.7), none on the summons for 1,354. By breed in
  a group, on the character: from x2.4 (Eliotrope, 16) to x6.7 (Enutrof, 3); the Forgelance (20) x6.6. The fights are
  short: 1.1 to 1.2 rounds a fight won for every breed but the Crâ's (9) 4 fights; the Féca (1) idle on 26 % of its
  turns, the Iop (8) on none; one death in all, an Iop's.
- **Made in scripts only** (D10, no test): `BreedSections` holds the sums by breed, `TurnBench` and `LiveBench` add to it
  and print its sections; the options are unchanged.
- **Rejected.**
  - The study's keys "zz breed N" in the sorted counts: a section by breed reads planned against played side by side.
  - A breed's name from the pack's texts: the tool prints ids, as the rest of the bench does (D196).
  - A switch to print the breeds: they are what every session of the AI reads, at the cost of some 270 lines for
    `--turns` and 60 for `--live` on these captures.
