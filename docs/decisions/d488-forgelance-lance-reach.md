# Fights, the Forgelance's spells cast on its lance (D488)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The lances searched
first are [D462](d462-forgelance-lances.md), the lance out of hand [D465](d465-forgelance-lance-state.md), the spell
modifiers [D193](d176-d208-step-9.md#d193-what-the-server-announces-of-a-fight-named-and-kept-mapping-format-10).

## D488: The lance gives each spell it names its reach, and a summon on a free cell asks its caster's conditions

*2026-11-21, the owner's report of a Forgelance casting a zone spell elsewhere than on its lance, the lead's session of
the six reports of 20 and 21 November. Refines D465. Builds D187, D193, D227, D462.*

- **Found in the pack of 3.7.14.18**: as the lance (monster 7139) comes, its starting spell makes the Forgelance cast
  24591 on itself, which sets the range of 13 of its spells to 1 to 63 (effects 2906 and 2905), drops their line of sight
  (289, for some) and asks a taken cell (314): Effondrement, Phalange, Épilogue, Terre du Milieu, Noa, Ydra, Prélude au
  Fer, Moulin Rouge, Fer Rouge, Moulin à Vent, Dégagement and two more; Muspel, which asks the lance out, its range. Each, its lance out (3361, not 3589), casts by
  effect 1160 a spell of its own on the lance: Effondrement's 30711 strikes a star of 2 around it, Phalange's 24806
  shields a circle of 2, Épilogue's 30712 recalls it. The Bubble emulator's lance mark (spells 23262, 24391) is the
  lance's own aura, Étendard, read for ideas only (D3).
- **Measured** on the 25 game captures of 20 and 21 November (one Forgelance, each cast counted once): with the lance out,
  Effondrement was cast on it 43 times and struck the enemies around it, and 8 times on an enemy, striking nothing, as
  the owner saw; Phalange 16 times on it; Épilogue 15 times on a free cell, doing nothing.
- **The engine's faults**: `Imagination` gave every modifier of 24591 to the first spell it names, 24219, so that
  Effondrement kept its range of 0 to 3 and reached the lance only from beside it; effect 314 was unknown; and the summon
  of Épilogue, of mask `a,A,*E3360`, came on a free cell whatever its caster's states, so that the AI cast it, the lance
  out, for a second lance.
- **Made**: a modifier names the spell its effect's first die names; `SpellModifierKind.TakenCellEnabled` (314) asks a taken
  cell; `TargetMask.CasterAllows` reads a mask's conditions on its caster, which every effect on a free cell asks; the
  lance's return takes back 24591's modifiers, as Armé dispels it. Six tests: a taken cell asked, the caster's conditions
  read without a target, each spell given its reach, the reach taken back, the AI throwing then striking around its lance
  from afar, no second lance while the first is out.
- **Replayed** with `scripts/fights.cs --turns` on the same captures, before and after, its new "lance out" section
  counting the casts on the lance, an ally, an enemy or a free cell: the casts planned on the lance 24 to 112
  (Effondrement 18 to 43, Phalange 6 to 67), Épilogue on a free cell 26 to 0, Effondrement on an enemy 3 to 0. The
  Forgelance's 671 turns: empty plans 66 to 52, walks alone 121 to 101, kills foreseen 179 to 184, damage this turn
  24,900 to 24,664, over two turns 67,392 to 68,703, kills over two turns 600 to 615, the reply 3,923 to 3,912, shields
  44,559 to 55,043. Every other breed the same line for line but the Pandawa, 4 casts of 1,173 turns: damage 49,554 to
  49,321, the reply 8,968 to 9,134. None of its spells holds an effect the change reads (no summon or teleport asking its
  caster, no modifier id twice in a level); the searches are bounded in time, on a machine shared with other replays.
- **Rejected**: a rule naming the 13 spells, the pack holding it; leaving 314 to the server's refusal (D407), a round trip
  and a plan counting on a blow it cannot strike.
- **Not measured in game**: the Forgelance striking around its lance from afar. **Found, left to the owner**: of the 81
  turns of these captures that ended with the lance on the map, 66 began the next with it there, against D465's return as
  the thrower's next turn begins (304 of 308 turns on 18 and 19 November).
