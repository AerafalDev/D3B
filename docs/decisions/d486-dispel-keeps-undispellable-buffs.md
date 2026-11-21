# Fights, a dispel keeps the buffs the server keeps (D486)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The buffs as the
bot's fight state keeps them are [D193](d176-d208-step-9.md#d193-what-the-server-announces-of-a-fight-named-and-kept-mapping-format-10)
and [D225](d209-d252-step-9.md#d225-a-buffs-end-mapping-format-13), a spell's buffs taken back
[D227](d209-d252-step-9.md#d227-the-spells-effects-cast-imagined-and-a-spells-buffs-taken-back).

## D486: A dispel takes only the buffs the server ends, and a removal naming a spell that spell's alone: the Pandawa keeps Sobre and casts in its element

*2026-11-21, step 10, row 10i, the owner's report from the group runs and the farmer of 20 and 21 November, in French: "un panda
fait un peu n'importe quoi au niveau de ses sorts (utilise souvent des sorts qui ne sont pas de son élément)". Refines
D193 and D225; builds on D420, D463, D474. The fix is the bot's fight state, general, no breed named.*

- **Measured first, the element of each blow** (`scripts/fights.cs --turns`, a new count by breed: the blow's element
  against the one its caster's characteristics raise most, and the damage foreseen). On the 17 game connections of
  21 November, the Pandawas (breed 12), whose best element is air, planned 187 of 475 blows in another and played 204 of
  474; the farmer alone planned 110 of 132 off its element, earth's 12803 and fire's 12781 above all, air's 12782 22 times.
  Their damage was foreseen right: `scripts/fights.cs` finds every cast of the Pandawa's spells within what the formulas
  foresee, 12782's 891 dealt against 842 to 963 foreseen. So neither the formulas nor a term of the judge.
- **The cause: the states the bot held.** At its turns' start, the farmer Pandawa held no state in 71 of its 85 turns:
  Sobre (3531), which it holds from the fight's start, was gone from round 2 on in 13 of its 14 fights. Each of 12782's
  damage effects asks Sobre or Saoul of its caster (masks `*E3531`, `*E498`), so the AI saw the air spell strike nothing
  and cast the earth and fire spells whose damage asks no state. The server held the Pandawa sober all along: 31 casts of
  12781 set state 3532, whose effect asks the caster sober, on a Pandawa the bot held in no state.
- **Why the bot let Sobre go.** `FightEvents` took every buff of a fighter on a removal whose flag reads "all": on a
  monster's dispel (action 132, spell 24577 for the farmer's enemies), and on the removals of a spell's buffs (action
  406) that carry the same flag. Measured on the machine's 216 game connections of 3.7.14.18, with field 9 of the
  client's buff read locally beside the pack's `dispellable`, which it gives less 1: of the 533 buffs held by a fighter
  dispelled 129 times, the server ended the 22 it holds dispellable one by one (`FightBuffEndEvent`) before the next
  turn, and 506 of the 510 not dispellable, Sobre among them, were still held then. The 441 removals of action 406 with
  the flag named a spell no fighter held buffs of (13996 440 times), and took the fighter's other buffs.
- **Made.** A removal naming no spell, a dispel, takes nothing itself: the server ends each buff it takes. A removal
  naming a spell takes that spell's buffs alone, its flag set or not. Tests (`FightEventsTests`): a dispel then the end of
  the one buff the server took leaves Sobre held; a removal naming a spell with the flag set leaves another spell's buffs.
  `scripts/fights.cs --turns` also counts, by breed, the blows' elements and the states held as each turn begins.
- **Replayed** before and after, the same captures, on main's 01ab93eb (`scripts/fights.cs`, `--turns`, `--live`). On 21 November, the
  Pandawas' 284 turns: no turn begun in no state (71 before); blows planned in another element 104 instead of 187, in
  air 384 instead of 288; 12782 planned 329 times instead of 233, 12803 2 instead of 76, 12781 9 instead of 36; damage
  foreseen 26,903 instead of 23,665, kills 80 instead of 62, the reply taking 4,966 instead of 6,671, 53,037 over two
  turns instead of 48,734 and 211 kills instead of 151. The farmer's 82 turns alone, its 14 aggressions fought alone, 11 of
  them lost (the owner: its fight AI, not its level): damage foreseen 6,059 instead of 2,742 (2,935 in the turns played),
  kills 18 instead of none, the reply taking 558 instead of 2,280, 60 kills over two turns instead of 2. The other breeds' damage, kills and reply are the same; their
  judged sums move by 44 at most. On 20 November's 113 connections, where no Pandawa lost its state, no breed's count
  moves by more than 1%: the Pandawa's 908 turns plan the same damage and kills, Karcham 428 times instead of 431; the
  reply foreseen on the character in a group is 27,634 instead of 27,586, the enemies keeping buffs the flagged
  removals of 406 took from them, such as spell 13996's power. The bench of the effects: the states a caster's cast removed that the engine
  did not foresee fall from 2,786 to 912, every damage count the same.
- **Not measured in game.** The live counts are what the bot did, the same before and after: the Pandawa's 11 fights lost
  of 1,119 and its 20 deaths on 20 and 21 November are not replayed, and whether the farmer now wins against the aggressions is to see in game,
  a Pandawa against monsters that dispel. The 104 blows still planned off its element are mostly throws of a carried
  fighter (12793 and 12811, for where they land) and pushes. The 4 buffs not dispellable the server ended after a dispel,
  and field 9's value 3 (8,396 buffs, the pack's 1), are not explained.
- **Rejected.** Reading field 9 of the buff, the server's own word on what a dispel takes: mapping format 25 to publish,
  where the server's ends already say it. Keeping the buffs the pack says are not dispellable: the bot's fight state reads
  no pack. A rule for the Pandawa, giving it Sobre back at its turn's start: a state the server never gave (D192, D244).
