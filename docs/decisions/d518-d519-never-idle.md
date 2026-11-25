# Activities, never idle until the server's kick: the recovery ladder and the run's restart (D518, D519)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The inactivity kick is
[D481](d481-harvest-losses-go-on.md); the busy character, [D484](d484-busy-wait-bounded.md); the loop through a mine,
[D488](d488-harvest-action-cells.md); the miner's door, [D517](d517-miner-refused-door.md); the recovery after an interruption, [D89](d064-d090-step-4.md); a walk the server holds,
[D456](d456-groups-one-cell-each.md) and [D464](d464-groups-held-follower.md).

## D518: A journey or a banking that fails climbs a bounded recovery ladder before it ends the activity: tried again twice, the character put in order, a step aside, out of its map by the nearest exit

*2026-11-25, the lead's task from the owner's ask ("le bug où on se fait déco à cause de mineur, ça fait plusieurs fois qu'il
arrive, faut le corriger une bonne fois pour toutes et pas du bricolage"), the design the session's within the task, the map's
information not asked again the owner's choice between the two the session put. Supersedes
in part D128 and D129 (three failed journeys, three bankings) and D484 (its failure ends the run). Builds D60, D74, D89, D456,
D464, D488, D517.*

- **The pattern**, four times in a week, each mended case by case: three fights lost in a row ended the harvest (D481); a step
  waited for ever on a busy character (D484); a tour looped through a mine with nothing done (D488); and on 2026-11-25 an
  unloading failed right after a walk to a mine's door was refused (D517). Each fix closed its own way to the kick and none the next:
  any other failure of a journey or a bank still ended the activity, and the character idled until the server closed the
  connection 30 minutes on.
- **Measured** on the owner's capture of 24 November 20:58 UTC (`…ADMCGBN6K`, `scripts/capture.cs --messages`, types and times
  only): at 03:32:26.418 D3B's walk, shown 30 ms later; at 27.532 the client's confirmation; at 27.560 the server's refusal and
  the confirmation's response together, and D3B's use of the door in the same millisecond, refused twice at 27.588; then no
  request of D3B's for the 29 min 56 s the capture still lasts. On the miner's capture of 23 November (`…9SG`, 14 h 15): 101 walks
  refused, 57 of them in the batch of the confirmation's response; 8 uses refused, 7 followed by a map change 2 s later, the
  harvest going on; D3B's longest silence 301 s.
- **Made**: `RecoveryLadder`, in `D3B.Bot.Activities`. Each failure climbs one rung before the action is tried again: twice
  from the state as it is; once the character was put back in order as after an interruption, a walk the server holds let go
  by a walk (`Recovery.SettleAsync`, D89, D456); once it stepped aside to a free neighbour, whatever the state says the server
  holds (D464); once it left its map by the exit nearest a walk away, scroll, door or map action, no zaap and no NPC
  (`MapChange.LeaveByNearestExitAsync`). A busy character is waited for 10 s at most before a rung. With no rung left the
  action fails, saying why; a success puts the ladder back at its foot.
- **Where**: the harvest's journeys, to the zone and through it, which fail the activity after six failures in a row instead of
  three (`FailUnreachable`, `FailLost`); the harvest's and the hunt's bankings, every outcome but done tried again, no bank
  found included, which ended them at once (D517's no path).
- **Not made, the map's information asked again**, the task's third rung: D60 has D3B never send what the client sends after an
  action, and the capture holds one request for a map's information per map the server named, 2,834 for 2,834. One of D3B's
  would be answered to a client that never asked, which nothing measured. The rung out of the map has the client ask on the
  next map instead, the owner's choice, measuring the request in a session of its own the other.
- **Rejected.**
  - A ladder per case, the bank's or the door's: the mending case by case this replaces.
  - The ladder inside a journey (`WorldTravel`): a journey already leaves out a way that failed and seeks another (D74); the
    ladder is for the journey that failed whole.
  - A zaap or the haven bag as the last rung: a zaap costs kamas and may be refused from the map (D248), and the haven bag
    leads back to the map it was entered from (D83).

## D519: A run that fails, or makes no progress for ten minutes, puts the character back in order and restarts its activity, after 30 s doubling to 8 min, six times an hour at most; a failure no restart would change, or a stop, still ends it

*2026-11-25, same task; the profile's setting on by default for the harvest and the hunt, the owner's choice between the two the
session put. Supersedes in part D481 (an activity that ends for good leaves the connection idle) and D484 (still busy a minute
after being put in order, the activity fails). Builds D2, D87, D89, D504.*

- **Why a net**: the ladder bounds the failures someone foresaw. A step that waits on what never comes, or one no one foresaw,
  a bug included, still ends or stalls the activity; the run is the one place every activity's steps go through.
- **Made, what fails**: a step's error; an unloading that did not unload, which ended the run still full; and no progress
  for `ActivityRestarts.NoProgress`, 10 min with no map changed, no fight begun and no pod gained or banked, the run's watch then
  cancelling the behavior it waits on. Not counted: the connection gone, the player's hand, a fight, the wait before a restart,
  and a wait the activity makes on purpose (`BotActivity.WaitingUntil`, the harvest's for its zones to grow back).
- **Made, the restart**, for an activity that `RestartsAfterFailure`: the character put back in order as after an interruption
  (`Recovery.SettleAsync`), a wait of 30 s doubled for each restart of the last hour, 8 min at most, then
  `BotActivity.Restart`, which begins it anew, its counts kept, the harvest's rotation drawn again and its tours begun again. The
  journal says so (`ActivityRestartedEntry`, D504); the card shows `Restarting` meanwhile, then why it restarted while the
  run goes on, and that it gave up once over (`ActivityRun.LastRestart`, `ActivityRun.GaveUp`).
- **The bounds**: a failure that `ActivityException.IsFinal` ends the run at once, as before: the zone gone from the project, no
  sub-area or map holding what the character can harvest or hunt, the character still full once banked. A seventh failure in
  the hour ends it too, saying it gave up: the character then idles until the kick, as D481 left it. A run stopped by the
  player, by the host or by its activity's answer to an interruption (a profile stopping after a fight, the hunt's third lost
  fight in a row, D198) is never restarted. 10 min with no progress, then the longest wait, still send a request within 18 min,
  under the server's 30.
- **The setting**: `RestartAfterFailure`, an advanced flag of the harvest's and the hunt's profiles, on by default; a profile
  stored before reads it on.
- **Tested** on the fake game (`HarvestNeverIdleTests`, `ActivityRestartTests`, `RecoveryLadderTests`): the miner's bank journey
  refused out of the mine tried again up the ladder and the banker reached; an unloading that never reaches a bank restarting the
  harvest; D484's use that never ends restarting it; a harvest whose every request goes unanswered sending one at least every
  8 min 04 s for 30 min, then failing, saying it gave up after six restarts. D481's losses already never end the harvest
  (`ProtectorsBeatingTheCharacterAgainAndAgainNeverEndTheHarvest`). Not seen in game.
- **Left**: D488's two captures are no longer on disk; a loop through maps is progress to the net, which D488's tour going round
  ends. A wait on purpose longer than 30 minutes, every zone growing back, still sends nothing. The hunt's journeys keep their
  own three failures, the net behind them. The groups' activities, a follower's, restart nothing.
- **Rejected.**
  - The setting off by default: the guarantee would hold only for the profiles a player changed.
  - Restarting from the host or the app, a new activity from its profile: the run would lose its record and counts, and a
    sequence of profiles (D421) its place.
  - Restarting at once, or without a cap: a failure that passes in minutes would use the hour's restarts in seconds, and a bug
    failing every step would restart for ever, the card never saying it stopped.
  - Map changes with nothing harvested taken for no progress: a tour crossing a zone of 186 maps is progress, and the loop is
    D488's tour going round.
  - Keeping the connection busy once a run ended: feigning a player, out of scope (D2, D481).
