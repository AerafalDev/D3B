# Hunting, the Minor Uchronic Elixir kept up (D501)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). An object used is
[D148](d126-d175-step-6.md#d148-upkeep-resource-bags-opened-the-character-healed-under-a-share-of-its-life); the pins
[D54](d034-d063-step-3.md#d54-the-game-is-paired-by-pins-where-its-structure-is-silent-then-from-build-to-build); the
mapping's format [D342](d293-d358-step-7.md#d342-a-characters-breed-and-sex-from-the-list-mapping-format-16-and-a-migration).

## D501: A hunt profile may keep a Minor Uchronic Elixir going, drinking one when its effect is missing or ends

*2026-11-22, a contributor's pull request (#375), to farm anomaly fragments; reviewed and completed by the lead on
2026-11-23, renumbered from the contributor's D499, which `main` had given the zaaps' discovery. Builds D54, D148,
D342.*

- **What was measured.** On the contributor's two game sessions of 22 November on 3.7.14.18, starting at 13:19:57 and
  14:04:59 UTC, read by structure with `scripts/capture.cs`: the server lists the character's alterations in `lxg` as it
  enters the game and between fights, a repeated record `lxj`; a use of the elixir is followed by `lxk`, one record of the
  same type; the record's field 2 is the alteration, 50 for the minor elixir, its field 3 the fights left, from 15 down to
  0, and its end comes with information 1665 naming the alteration in its first parameter, while a snapshot may still list
  it at 0. On the owner's 239 game connections of 18 to 22 November, the lead found the snapshot at each of the 222
  entries and after each of the 8,727 fights' ends, the first message of the kind after it, and always empty: neither the
  fields nor the grant could be checked there (client-game.md, "The game, altered").
- **What was made.** The readable protocol gains `AlterationsSnapshotEvent`, `AlterationAddedEvent` and
  `RoleplayAlteration`, pinned for 3.7.14.18 (D54), and the text event's parameters: mapping format 25, which
  `scripts/mapping.cs` writes on the owner's machine with the owner's captures, 18,376 bytes, as on the contributor's. The
  character's state keeps what is left of each alteration by its id, and whether a snapshot came, so that an alteration
  unknown is told from one absent. The hunt's flag « Farmer les fragments d’anomalie », off by default: before its first
  fight it waits at most an answer's delay for the snapshot, then, before each step and each attack, drinks one carried
  elixir (item 20962) when alteration 50 is absent or at 0, through the object's use of D148, and waits for the grant.
- **A use not honoured.** A use the server refuses, does not answer, or answers without the grant counts as a failure; a
  granted one sets the count back to 0, and after 3 in a row the run stops trying, so that the hunt never sends a request
  before every fight for nothing and never drinks a second elixir while the first's grant is on its way.
- **Rejected.** Trying an elixir after every fight, which sends a use for nothing during the effect's 15 fights. A hook
  through which the server's texts reached the running activity, as the pull request first had: the character's state
  already turns information 1665 into the alteration at 0, which the hunt reads. The elixir's item and alteration read
  from the game's data: two ids measured are enough for one object; a second object of the kind would bring it.
