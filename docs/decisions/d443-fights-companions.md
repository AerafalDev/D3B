# Fights, the companions (D443)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D443: The bot plays the character's companion, such as the Astrub Knight, with the spells the server lists for it, mapping format 21

*2026-11-18, outside the roadmap's steps, a player's report relayed by the owner ("le bot prend pas en compte le chevalier
d'astrub"); playing the companion chosen by the owner as the lead recommended it, the owner measuring it in game. Builds
D193, D271, D279.*

- **Found.** The bot played the turns of the character and of the summons it controls alone (D271): a fighter whose
  `SummonerId` is the character's. A companion is neither: the server shows it as a fighter of a third kind, which D3B did
  not read, so the bot waited through each of its turns until their time ran out, and the AI saw it as an ally with no
  spells. No monster of the pack is the Astrub Knight, and the pack has no companion table.
- **Read from the client's schema alone** (3.7.14.18, by structure, nothing written down from it): `FightFighter`'s kind is
  one of three, a character (5), a monster (6), and a third (11) of a string, an int64 and two int32, the companion: its
  int64 is taken for its master, as a summon's summoner is an int64. `FightSpellsContextEvent` lists the spells of a fighter
  the client plays, the fighter named apart (D279).
- **Made.** `CompanionFighter` (`master_id`) in `actors.proto`, mapping format 21 of 3.7.14.18 (255 messages and 556
  fields, no failure, the same bytes twice). `Fighter.MasterId`; the client plays a turn the server lets it play of the
  character, a summon of its or a companion of its (`FightState.IsOwnTurn`), and `FightPlay` plays it as a summon's.
  `FightState.ListedSpells` keeps the spells the server lists for a fighter other than the character, and `ImaginedFights`
  gives them to a fighter the pack does not know; a summon keeps its monster's (D193). A companion whose spells the server
  never listed walks at most, then ends its turn at once. The engine knows the companion's master (`Combatant.MasterId`),
  and the AI playing the companion protects its master, as a summon playing protects its summoner (`FightJudge`).
- **Not measured**: no capture holds a companion. Whether the server lets the client play the companion's turn
  (`FightTurnStartPlayingEvent`), lists its spells, takes the character's requests as the companion's as it does a
  summon's (9f2), and what the string and the two int32 hold, are left to the owner's measure in game: a fight with the
  Astrub Knight, captured.
- **Needs**: the mapping of format 21 published with the release `client-3.7.14.18` before an app that reads it, on the
  owner's go (D24).
- **Rejected.**
  - Ending the companion's turn at once without playing it: small and safe, but the companion would never strike.
  - Measuring first, no code before a capture of a fight with the companion: the most rigorous, but nothing changes for the
    players meanwhile; the measure comes with the fix instead.
  - A companion table extracted into the pack for its spells: more data, while the server lists them for the fight.
