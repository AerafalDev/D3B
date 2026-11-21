# Fights, the turn's pace (D479)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The fight's pace
is [D202](d176-d208-step-9.md), then 0.8 s in [D368](d366-d368-step-9.md).

## D479: The fight's pace 0.3 s, and no pace before the turn's end

*2026-11-21, step 10, the owner's report from game: "the characters should play their turns a little faster"; the owner
chose 0.3 s among 0.3 and 0.5 s. Supersedes in part D368 (the pace of 0.8 s), refines D202.*

- **Measured** on the owner's captures of 2026-11-21 (`scripts/capture.cs --messages`, structure only, the fights of
  four connections, 293 of the AI's turns): a turn took 3.7 to 5.2 s halfway for 2 to 3.5 actions. The first action
  went 0.82 s after the server's turn event, each next one 1.32 to 1.45 s after the last, and the turn's end 1.13 to
  1.43 s after the last action: the pace of 0.8 s, then the client's display of the action it waits for, about half a
  second.
- **Made**: `BotDelays.FightAction` is 0.3 s, still the least the settings allow, raised in them by whoever wants to see
  more of each action; every action still waits for the client to have shown the last first. The turn's end no longer
  waits for the pace once the turn acted: the AI ends its turn as soon as its plan holds nothing more, unless the fight
  ended or its turn did meanwhile. A turn with no action keeps the pace before its end, and so does a turn that killed
  the last enemy, whose fight the server may end within it (D203). A turn of three actions should take about 1.5 s less.
- **Rejected**: 0.5 s, the owner's other option, slower for little; no pace at all, which would send each action as
  soon as the client showed the last, faster than any player and left out of the owner's options; waiting less for the
  client's display, which the client needs to follow D3B's actions (D60).
