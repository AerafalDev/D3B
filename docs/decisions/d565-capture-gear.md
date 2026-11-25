# Hunting, capture nets and soul stones worn (D565)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The mounts' capture
it completes is [D533](d533-mount-capture.md#d533-a-hunt-profile-may-capture-wild-mounts-the-main-wears-a-carried-capture-net-casts-its-taming-spell-on-a-mount-at-its-first-chance-and-hunts-on-without-capturing-once-it-has-none).

## D565: The hunt wears the best capture net or the smallest soul stone that suffices before a group to capture, and casts the soul capture against an archmonster

*2026-11-25, the capture gear session, at the owner's ask (« faut qu'on fasse la mesure pour equiper des pierre d'ame/filet
de capture et puis le faire quand l'option est coché avant la prochaine release », then « faut tout prendre en compte y'a
plusieurs pierres et plusieurs filets »); each choice below the owner's. Builds D533, D501, D148.*

- **What was measured.** On the owner's capture of 2026-11-25, client 3.7.14.18, the equipping done by hand while the app
  relayed: the client's request `ito`, which the readable protocol lacked, names a quantity (field 1), a stack (field 2)
  and a position (field 3); the server answers within 30 ms. A capture net and a soul stone go to position 31, the fight
  consumable, one stack at a time: the whole stack named moves there; part of one becomes a new stack there, or adds to the
  stack of the same item worn; the stack worn before goes back to 63 in the same answer. A net worn adds its taming spell
  to the character's spells, taken off removes it; a stone gives none, the character knowing spell 413 already. The 233
  requests of the owner's 82 earlier captures, equipment worn and taken off, all answer so.
- **What was made.** The readable protocol gains `ObjectSetPositionRequest`, pinned to `ito` for 3.7.14.18: mapping format
  26. `ObjectWear` wears a whole stack at a position and waits for the stack there. Before attacking a group, the main of a
  hunt, never a follower, wears in slot 31 what the group calls for, unless it is worn already: with « Capturer des
  montures » and a wild mount of a species ticked, the best net carried that fits it, the one whose Éleveur level needed is
  highest among those the character's level allows (`MountCapture.Nets`, the ten of D533), the universal last; otherwise,
  with the new « Capturer les âmes d'archimonstres », off by default, and an archmonster in the group, the smallest stone
  carried that captures the group's highest level (`SoulCapture.Stones`, the five crafted ones of D533). With none that
  fits, the hunt attacks without capturing and the journal says so.
- **In the fight.** Against an enemy archmonster, dead or alive, the main casts spell 413 on itself whenever the rules allow
  it, before the blows, as the taming comes first (D533); with no stone worn, the state 2768 it needs is missing and the
  rules refuse it. A capture the server does not take is not tried again in that fight. A species' net tames only its
  species: the mounts of another are struck as any monster.
- **Not measured.** No fight with a net or a stone worn: whether the server lists the spells as the fight starts, the
  casts' answers, the fight's end with a mount or a soul, the stack lessened. The capture's state lasts 2 turns and the
  spell comes back every 3, so a fight that ends on the third turn after a cast captures no soul: the owner's choice of a
  cast whenever possible leaves it so until a fight measures it.
- **Rejected.** Wearing one object of a stack: the slot would empty after each net used, measured to take a stack whole.
  A stone worn for a plain group, or souls of dungeon bosses: the owner chose the hunt's archmonsters only, the dungeons'
  bosses left for their session (D520 to D529). The largest stone always: it would spend the dear stones on low levels.
  Wearing the stone only, the player casting: no use for a bot alone. Taking the net or stone off after the fight: the
  slot holds nothing else a hunt needs. A new pack format for the items' criteria and effects: the fifteen items' facts,
  measured in the client for D533, are a table of the code, as the taming spells are.
