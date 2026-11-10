# Bug reports, BR1 (D420)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D420: Two settings for the captures: the game is recorded unless turned off, every connection only when asked; the retention is applied whether or not the game is recorded

*2026-11-10, step BR1, the lead's (reversible, settled alone, the owner to confirm). Refines D409, D37.*

D409 says "`Network:Captures:Enabled` on for the game", but `Enabled` already means "every connection, the connection phase's too, which holds the
account's login token" in the README, the mapping tool's notes and the owner's habits.

- **`Network:Captures:Game`**, on by default, records the game connection in `captures\game`. **`Network:Captures:Enabled`**, off by default,
  keeps its meaning (every connection) and so records the game's too. The Network settings' toggle is `Game`; turning it off also forgets an
  older `Enabled`, which would go on recording.
- **The retention** (`Network:Captures:Retention:Days` and `:Megabytes`, 7 and 1,024, 0 lifting a limit, bounded to 3,650 and 1,048,576) is
  applied at each start of the host to `captures\game` even when the game is no longer recorded: a capture left on disk holds a login ticket.
- **Rejected.** Making `Enabled` mean the game only: the mapping tool's workflow and the README would change meaning under their readers.
  Applying the retention only while recording: the captures of a player who turned recording off would stay for ever.
