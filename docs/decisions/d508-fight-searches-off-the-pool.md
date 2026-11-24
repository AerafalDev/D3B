# Fights, the AI's searches off the thread pool (D508)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The bot plays its
fights with the AI from [D201](d176-d208-step-9.md) on, and runs the bots' host in the studio's process from
[D91](d091-d102-step-5.md).

## D508: The fight AI searches on threads of its own, half the cores and below the normal priority, never on the thread pool

*2026-11-24, a player's report of Kestrel's warning, "the heartbeat has been running for 00:00:01.08 which is longer than
00:00:01. This could be caused by thread pool starvation"; the lead's choice. Builds on D9, D91 and D201.*

- **Found.** `FightPlay` called `FightTactics.Plan` synchronously on a thread pool thread (the pilot is `Task.Run`), a
  CPU-bound search of its 800 ms budget or more, again after every action of a turn; the odds and the placement too. The
  relay's reads and writes run on the pool. The second suspect, `BehaviorActivity` waiting on a rule's action with
  `GetAwaiter().GetResult()`, never blocked: a rule plays only instant actions, whose task is already complete.
- **Measured** with a throwaway harness (git-ignored): the 331 turns of the owner's 4 captures of 24 November from 04:16
  planned by N bots at once, the bot's 300 ms pace between plans, the process held to 4 cores with `DOTNET_PROCESSOR_COUNT=4`,
  while a dedicated thread sleeping 100 ms measured its lateness, as Kestrel's heartbeat, and a work item queued to the pool
  every 100 ms measured its wait. On the pool, 4 bots: the pool's wait 11 ms halfway, 1,767 ms at most, 2 waits over a
  second in 20 s; 8 bots: 3,858 ms halfway, 10,357 ms at most, 7 over a second in 30 s. On 2 dedicated threads below the
  normal priority, 4 bots: 0.1 ms halfway, 50 ms at most; 8 bots: 0.1 ms halfway, 79 ms at most, none over 100 ms. The
  heartbeat's own lateness at most 134 and 216 ms on the pool, 79 and 258 ms on the threads, the garbage collector's
  pauses (0.9 to 3.3 s in 30 s) weighing on both.
- **Made.** `FightSearches`, one per host, made by `Bots` at the first fight and disposed with it: `Math.Max(1, cores / 2)`
  background threads below the normal priority take the searches in the order asked; what awaits a search goes on on the
  pool (`RunContinuationsAsynchronously`), and a cancelled wait drops a search not yet begun. `FightPlay` runs the odds, the
  placement and every plan there. A rule's instant action is applied through `IInstantNode`, with no task waited for.
  Tests (`FightSearchesTests`): a search runs off the pool, below the normal priority, no more at once than the threads, a
  search that throws fails its task alone, one cancelled before its turn never runs.
- **The search's time.** The same harness showed searches of 2 s halfway and up to 5.5 s on the pool, 9.8 s on the threads,
  against a budget of 800 ms: the budget is not a limit of the whole search (D509).
- **Rejected**: raising the pool's minimum threads, which leaves the searches at the relay's priority and only delays the
  starvation to more bots; a search on `Task.Factory.StartNew(LongRunning)`, a new thread at each of the thousands of
  searches of a session and no bound on how many run at once; as many threads as cores, which leaves none to the relay, the
  studio and the game clients the player runs beside; a search at the normal priority, which competes with the heartbeat.
