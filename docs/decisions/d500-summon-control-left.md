# Fights, the summons' control left as the player set it (D500)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The summons'
control is [D269](d253-d292-step-9.md#d269-summons-measured-characteristics-turns-and-control) and
[D271](d253-d292-step-9.md#d271-the-bot-plays-the-summons-the-client-controls); the states kept in the search,
[D255](d253-d292-step-9.md#d255-a-change-of-the-fighters-states-kept-in-the-turns-search).

## D500: The AI never casts a spell every character has that switches its summons' control: no more "Maîtrise des invocations" for nothing

*2026-11-22, the owner's report from game: "l'huppermage spam maitrise des invocations alors que ça sert à rien". Builds
D255, D269, D271, D407.*

- **Found** with `scripts/fights.cs --turns` on the owner's 18 game captures of 22 November: not the Huppermage alone, every
  breed of the group planned 18646, *Maîtrise des invocations*, the Huppermage 14 times in 215 turns (26 cast by D3B), the
  Forgelance 9 in 235, the Pandawa 6 in 323, the Sacrieur 3 in 300. A throwaway probe on the plans that held it: every
  character began its turns in state 2131, the control on, so that each cast turned it off; the cast is free, its 1 AP given
  back by its effect 120, and the judge weighs it at nothing (2,854.50 before and after, the same on all 17 plans). But it
  changes the caster's states, so the search kept the sequences starting with it beside its best ones (D255): free beam room
  that crowded out other sequences, a plan with it sometimes found better and sometimes worse than the search without the
  spell (984 against 1,195 on one turn of the Forgelance's).
- **Made**: before each search, `FightTactics.Plan` takes out of the character's spells those every character has (a
  variant of breed 19) whose casts reach effect 2027, the summons' control, through the spells they cast, three deep:
  18646 and its pair *Liberté des invocations*, 22312, on the pack of 3.7.14.18, and no breed's spell; the Sram's double,
  12915, which reaches 2027 too, is a breed's and still cast. The control stays as the player set it, which the bot plays as
  it finds it (D271). Four tests (`SummonMasteryTacticsTests`), on a mastery free in AP so that the small search would cast
  it, where the pack's gives back the AP it costs: the mastery cast twice leaves the caster as it was, a character never
  casts it nor its pair even where their AP would let a second blow come, a breed's spell that reaches the control as the
  Sram's double does is still cast, a character holding the mastery strikes; the second and the fourth red without the
  filter, the third red without its check of the breed.
- **Replayed** with `scripts/fights.cs --turns` on the 14 captures of 22 November closed before the replay, against `main`:
  the mastery planned 0 times instead of 10 by the Huppermage in its 192 turns (D3B cast it 19), 7 by the Forgelance, 5 by
  the Pandawa and 3 by the Sacrieur; the damage foreseen over two turns 50,524 to 50,927 for the Huppermage, 72,849 from
  68,892 for the Forgelance, its kills 342 to 372, 76,593 to 76,868 for the Pandawa, 96,190 to 96,881 for the Sacrieur, its
  kills 478 to 491; the life the reply takes 4,895 to 4,942, 10,980 to 11,118, 7,175 to 6,861 and 13,249 to 13,454; the
  plans' judgement down 0.1% to 0.2% for every breed, the beam room the free casts gave gone. Again on the 4 captures of
  the night of 22 to 23 November, 2,238 turns: the mastery planned 0 times instead of 92 by the Huppermage in its 396
  turns (D3B cast it 59), 39 by the Pandawa, 14 by the Sacrieur and 8 by the Forgelance; the damage foreseen over two
  turns 171,118 to 171,470 for the Huppermage, 296,196 to 293,716 for the Pandawa, 192,469 to 193,107 for the Sacrieur,
  its kills 81 to 112, 74,326 to 76,386 for the Forgelance, its kills 42 to 51; the life the reply takes 24,893 to 25,723,
  48,502 to 46,872, 61,396 to 60,966 and 20,441 to 20,619; the judgement down 0.5% for the Huppermage, level or up for
  the others.
- **Not measured in game**: a character of a group no longer casting the mastery, the control kept on for an Osamodas or a
  Sram.
- **Rejected**: a cast judged worthless left out of the search, which would drop a Zobal's mask too, its worth in the spells
  it opens a cast later (D255); the mastery searched only by the breeds that summon, as D408 searches their summons, since
  the bot plays its summons as the client controls them and has no use for the switch; a list of spell ids, where the pack
  says what the spell does (D192).
