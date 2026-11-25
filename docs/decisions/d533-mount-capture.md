# Hunting, wild mounts captured (D533)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The fight's spell
list is [D218](d209-d252-step-9.md#d218-the-weapons-blow-measured-on-the-captures); a worthless cast left out of the
search, [D500](d500-summon-control-left.md#d500-the-ai-never-casts-a-spell-every-character-has-that-switches-its-summons-control-no-more-maîtrise-des-invocations-for-nothing).

## D533: A hunt profile may capture wild mounts: the main wears a carried capture net, casts its taming spell on a mount at its first chance, and hunts on without capturing once it has none

*2026-11-25, the captures session, at the owner's ask (« une option pour capturer des muldo/dd/volkorne et capture d'âme
aussi pour les archis/donjons »); each choice below the owner's. Builds D218, D500, D501.*

- **What was measured.** On the installed client 3.7.14.18 (study `artifacts/studies/captures.md`, git-ignored): since
  3.5 there is no capture spell of its own; a net of type 99 worn in the fight consumable slot gives a temporary spell,
  32260 "Apprivoisement de monture" for the universal net 32521 (no use criterion, crafted at Éleveur level 1 from 10
  Bois de Frêne and 10 Fer), 32270 to 32278 for the species' reinforced and multiplying nets (Éleveur, job 79, above 99,
  149 or 199). The spell costs 1 AP, reaches 1 to 7 cells, is cast once a fight, only while the fight holds a wild
  Dragodinde, Muldo or Volkorne, on one not yet marked; it uses the net and marks the target, captured "at the fight's
  end", with no draw in any of its effects. The wild mounts are 12 monsters of races 21, 155 and 183, levels 62 to 70,
  in sub-areas 235, 170, 1119 and 886.
- **Not measured.** None of the owner's 74 captures holds a net or its spell: the request that wears an object in the
  slot, whether the server lists the temporary spell as the fight starts, its answers, the fight's end with a mount, the
  item the mount becomes and whether the slot keeps a stack. A capture of a fight with a net worn is owed before the
  wearing is built.
- **What is decided.** Mounts come before souls. The hunt profile gains « Capturer des montures », off by default, with
  the species to capture, all three ticked. Before each fight against a group holding a wild mount of a species ticked, the
  main wears in the slot the best carried net its Éleveur level allows when the slot holds none; with none carried, it
  fights without capturing and says so in the journal. In the fight, the AI casts the taming spell at the main's first
  turn with a target in reach and sight, before any attack, walking to it if needed, and no blow is planned to kill a
  target before it is marked. The groups attacked stay the profile's.
- **In a group, the main alone captures.** One mount a fight, several with a reinforced net; the followers spend no net.
  Only the main's own turns spare the mount: a follower's or a summon's casts may still kill it before the main's first
  chance, which a follower told the main's mounts would prevent, left for a later step.
- **What was made.** The hunt's flags, the main's taming before any blow (`FightTactics.Taming`), the mounts spared by the
  main's search (`FightKnowledge.Spared`), the journal's warning with no net worn, and a taming the server does not take
  ending the fight's capture, its mounts no longer spared. The net worn from the bag waits for the capture above.
- **Rejected.** Buying or crafting a missing net: D3B has neither until step 11, which will bring it. Stopping the
  activity once the nets run out: the hunt is worth going on with. A net worn by hand only: the slot may empty after each
  net used, which would ask the player to refill it fight after fight. Casting on the last enemy alive: the target might
  die first, for no gain measured. Every member with a net capturing: the owner wants the followers' nets kept. Attacking
  only groups with a mount while the option is on: the profile's choice of groups already does it through its families.
