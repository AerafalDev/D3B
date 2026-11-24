# Fights, the Chaferfu a last resort and Saoul's share dispelled (D503)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The summons as a last
resort are [D408](d407-d408-step-9-the-players-reports.md#d408-a-summon-is-a-last-resort-but-for-the-breeds-made-of-their-summons)
and [D462](d462-forgelance-lances.md#d462-the-forgelances-summons-are-not-a-last-resort-its-lances-striking-as-they-are-thrown);
the Pandawa's drink, [D420](d420-step-9-the-pandawas-drink.md#d420-a-state-the-fighter-already-held-is-no-gain-when-a-cast-puts-it-back-the-pandawa-no-longer-swaps-sobre-and-saoul-instead-of-striking)
and [D496](d496-damage-share-dispelled.md#d496-a-share-or-a-reduction-of-the-damage-received-is-held-by-its-spell-so-that-a-dispel-takes-it-back-the-pandawa-no-longer-drinks-three-times-for-one).

## D503: A summon every character has is a last resort whatever the breed, Saoul's share is dispelled with it, and the states a turn began with stay held through the turn

*2026-11-24, the owner's report from game: "les persos invoquent trop de chafer + le panda fait encore trop de fois
bombance". Refines D408, D420, D462 and D496.*

- **Found, the Chaferfu.** *Invocation de Chaferfu* (373) is a spell every character has (a variant of breed 19). On the
  owner's 4 captures of 24 November from 04:16, where D3B played 141 fights, the Forgelance cast it 24 times in 159 turns,
  the Sacrieur and the Huppermage once each; the replay planned it 41 times for the Forgelance. D462 searches the Forgelance's
  summons from the first search for its lances, and the Chaferfu came with them. On the 4 captures from 13:16, which the
  owner played by hand, the replay planned it 8 times in 39 Forgelance turns where its player never cast it.
- **Found, Bombance.** The same captures' Pandawa cast Bombance 84 times in 129 turns where the replay, planning once a
  turn, planned it 47 to 60 times. A throwaway probe replaying each turn as the bot plays it, planning again after each
  action, planned it 104 times in 137 turns, many turns ending drunk, sober, drunk. Two causes:
  - Saoul (24036, cast by Bombance) gives its 85% of the damage received with no rounds of its own, waiting for damage, so
    that `Imagination` never held it by its spell (D496 holds only a share that lasts): Sobre's dispel left it and each swap
    to drunk added one more, the pack's Bombance cast five times leaving 85, 85 and 85. The server's buff of it is held by
    24036, as the captures show.
  - The bot plans again before each action, and the state the Pandawa had just left was no longer one it held as that
    search began, so that swapping back was a gain again (D420's rule holds within one search only): on one turn, sober with
    2 AP the drink was worth 0.9, drunk with 1 AP the swap back to Sobre 12, the AP given back, and the drink again 0.9.
- **Made.** `FightTactics` keeps a summon every character has a last resort for every breed, the Forgelance's lances and the
  Osamodas', Roublard's and Steamer's own summons still searched first (`IsLastResort`). `Imagination` holds a share of the
  damage received by its spell whatever its rounds. `FightKnowledge.TurnStates` holds the states the character began its
  turn with, which `FightPlay` gives every plan of the turn, and `FightJudge` holds them as it holds those the search began
  with. Tests: a Forgelance strikes rather than summon what every character summons and still summons it with nothing else
  to do (`SummonTacticsTests`); a dispel takes back a share with no rounds of its own (`DamageShareDispelTests`); a state the
  turn began with, put back by a later plan, holds no worth, and one it did not begin with keeps it
  (`StateToggleTacticsTests`, on a Bombance built as the pack's). The first, the third and the fourth red without the change.
- **Replayed** with `scripts/fights.cs --turns` and the probe on the 4 captures from 04:16, against `main`: the Forgelance's
  Chaferfu planned 0 times instead of 41 (once instead of 35 planning again), its damage foreseen 31,114 to 36,480, its kills
  63 instead of 52, the life the reply takes 8,468 to 8,775; the Pandawa's Bombance planned 39 times instead of 60, and 58
  instead of 104 planning again as the bot does, its damage foreseen 36,441 to 35,882 and dealt planning again 38,808 to
  38,625, the life the reply takes 5,484 to 5,684, no phantom share left to soften it; the Sacrieur and the Huppermage did
  not move; every breed's damage foreseen 138,680 to 143,487. On the 4 captures from 13:16: the Chaferfu 0 instead of 8, the
  Forgelance's damage foreseen 18,813 to 19,748, the Pandawa's Bombance
  5 instead of 7, and 14 instead of 23 planning again (with the share dispelled alone) where its player drank twice in 29
  turns, its damage foreseen 10,252 to 10,303.
- **Not measured in game**: a group's characters bringing no Chaferfu while they have something else to do, and a Pandawa
  drinking once at a turn's end. The searches are bound by time, so that two replays of the same captures differ by about a
  third of a percent.
- **Rejected**: leaving out the Chaferfu by its id, a rule by spell (D192); a limit of Bombance's casts a turn (D420, D496);
  valuing a swap by the states it takes off as well as those it puts on, D420's more exact reading, which the Zobal's masks
  move too and their rounds unknown from the server; the bot planning once a turn instead of before each action, which
  D203 and D407 rely on to weigh what the server answered.
