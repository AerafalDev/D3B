# Step 9, the Pandawa's drink (D420)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D420: A state the fighter already held is no gain when a cast puts it back: the Pandawa no longer swaps Sobre and Saoul instead of striking

*2026-11-10, step 9, a player's report relayed by the owner, in French: "sur les panda, l'IA spam picole et tape quasi jamais". Refines D209
(buffs and states worth what they do over the rounds they last) and D407; builds on D192, D201 and D244. The fix is the judge's, general, no breed named.*

- **What Picole is** (the pack, client 3.7.13.17). *Bombance* (spell 12804, Pandawa, from level 5) and its variant *Picole* (12780, from
  level 115) cost 1 AP, target the caster, and may be cast 4 to 6 times a turn. Each casts, by two effects of mask `C,E3531` and `C,E498`,
  the state spells of the Pandawa's two states: *Sobre* (3531, whole fight) when it is drunk, *Saoul* (498, 2 rounds, which also puts
  state 1 on, takes 1 MP off and a share off the damage it takes) when it is sober, so it swaps them. Bombance gives the AP back when
  the caster was drunk; Picole gives 1 MP then instead. Spells ask these states: *Fermentation* (a shield), *Souillure*, *Lien
  Spiritueux* and *Pandanlku* ask Saoul; *Karcham* (carrying) and *Stabilisation* ask Sobre.
- **The cause: reproduced.** `FightJudge.Lasting` credits a buff the search imagined by the damage it adds over the rounds it lasts, the
  fighter with the buff against the fighter without it. A fighter's own state as the search starts is no buff, so it is worth nothing:
  the Pandawa sober by the server's word is worth 3,749.5 on the pack's data (a level 150 against two monsters), and the same Pandawa
  sober because a cast put Sobre back, 3,840.6 to 3,876.1 (+91 to +126), Sobre lasting the whole fight. So drunk, then sober by two casts
  was a gain of about 90 for 1 to 2 AP, more than a blow dealing 40 to a monster with 300 life. And the bot plans again before each
  action and plays the first (`FightPlay.PlayTurnAsync`): once the first cast was made, the state was the server's and the gain was
  gone, the search saw the same gain from there and cast again, up to the spell's casts a turn. It is D209's weighing counting a lasting
  effect each time it is cast again while it is still active, for the one effect that does not stack, a state.
- **Made.** `Lasting` leaves out a state buff whose state the fighter held as the search began. A state it did not hold keeps its
  worth: a drunk Pandawa swaps to be sober and strike, once. Tests (`StateToggleTacticsTests`, a pack-like swap of two states on the
  fight built by the test): a sober character holding a blow plans the blow where it planned two swaps; the swap back holds no worth;
  a drunk one that can only strike sober swaps once then strikes; replanning after each cast, as the bot does, swaps once or never and
  strikes.
- **Replayed** on the turns of the 48 captures (9,259 searches) of this machine (`scripts/fights.cs --turns`; no Pandawa among them, the
  numbers before in brackets). The AI's plan dealt more damage than the turn played in 2,205 turns (2,215), as much in 1,853 (1,850),
  less in 547 (540); kills as many 4,556 (4,556), more 33 (33), fewer 16 (16); the reply took more of the life in 871 turns (869). The
  casts planned: damage 6,998 (7,008), shields 1,230 (1,228), heals 154, boosts 370 (299), moves 202 (280); the damage foreseen over
  two turns 816,842 (816,994) and 755 kills foreseen (753). Only one breed moves, the Zobal (14), whose masks swap states by the same
  mechanism: the casts planned changing its states 295 (224), and the turn's last cast one that changes them in 133 turns (56) where
  the swaps between casts fall from 45 to 39. That reads as the Zobal no longer going back to the mask it started with at the end of a
  turn for the same credit the Pandawa found, and staying in the one it put on for the blows it asks: an inference from the counts, the
  Zobal's fights not read cast by cast, and not seen in game. No other breed's counts by spell move but by a few casts.
- **Not measured in game, and what was not proven.** No capture holds a Pandawa casting Bombance or Picole (the 48 hold breeds 1, 5, 8,
  10, 11, 14 and 16), so what the server sends back of a swap is read in the pack, not seen: that Saoul and Sobre are what the engine
  gives after a cast, that the AP is given back, that state 1 does no harm. The report is reproduced on the imagined fight only; the
  owner's friend is to say whether the Pandawa strikes now. What remains: a drunk Pandawa still swaps to be sober at once (a gain of the
  same kind, once, for the state it did not hold), which suits the carrying and Sobre's spells; and the value of being drunk is
  only what D209 reckons: Saoul's share off the damage the Pandawa takes waits for a hit (trigger `D`) and the imagination does not count it.
- **Rejected.** A limit of casts for the swap, or the spell named: a rule per breed (D192, D201). Counting the fighter's own states at the
  search's start as buffs of the full fight: their rounds are unknown, the server giving the state and not its end. Valuing a swap by
  the difference of the states before and after (the state it takes off against the one it puts on): the more exact reading, which
  moves the Zobal's masks' worth too, and wants the replay of the Zobal's fights the captures here do not hold.
