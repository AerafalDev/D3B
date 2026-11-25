# The journal kept on disk (D504)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The journal in memory
[D112](d110-d125-step-5.md#d112-a-bots-journal-says-its-actions-not-the-protocol) and
[D113](d110-d125-step-5.md#d113-the-logs-and-the-frames-kept-in-memory-always); the bug report [D410](d409-d411-bug-reports.md#d410-a-bug-report-is-a-d3breport-bundle-sent-by-a-private-discord-webhook)
and the captures' retention [D409](d409-d411-bug-reports.md#d409-the-game-connection-is-recorded-by-default-with-a-retention).

## D504: Each bot's journal and the host's problems are written to disk for seven days and 250 MB, saved from the studio and joined to a report for their last 24 hours

*2026-11-24, U2 of the next release's scope, the retention and the report's content chosen by the owner, the rest by the lead.
Builds D112, D113, D114, D409, D410. Refines D410's journal part, which was the 2,000 entries in memory.*

- **What was found.** A miner disconnected overnight was diagnosed only afterwards, from captures: its journal was a ring of
  2,000 entries in memory (`BotActor.JournalCapacity`), the host's problems the same (`HostProblems.Capacity`), and the only
  way out was the clipboard. After a night the cause of a stop has scrolled away, and a restart loses it.
- **What is written.** A `JournalRecorder`, a hosted service of the bots' module, reads every second what each bot's journal
  and the host's problems got since its last pass, as the logs page does, and appends it to `%LOCALAPPDATA%\D3B\journals`
  (`Journal:Directory`): a folder a source, `<account>-<character>` or `host`, a file a UTC day, `yyyy-MM-dd.txt`. No bot's loop
  waits for a disk; a crash loses at most the last second, and the host writes once more as it stops.
- **The line.** One an entry: its UTC time to the millisecond, then the record as it prints itself, in the invariant culture,
  indented by its depth, line breaks made spaces; a problem gives its level, its log, its message and its exception after a bar.
  It is the maintainers' text (D410), the same in a report, a saved file and on disk, and a time starts every line so that a
  span is read without parsing the rest. The trace stays in memory: it is 1,000 decisions a bot and was not asked for.
- **Retention, the owner's numbers.** Seven days, as the captures, and 250 MB, applied as the host starts and at each new UTC
  day, the oldest files first (`Journal:Retention:Days`, `Journal:Retention:Megabytes`, 0 for no limit, settings of the
  configuration only, no page yet). Estimated, not measured: about 1,000 entries an hour of about 150 bytes, 3.6 MB a day a bot,
  so ten bots round the clock fill the 250 MB in about a week. A day's file stops at 20 MB with a line saying so, so that a bot
  writing in a loop cannot fill the disk before the next sweep.
- **Saved from the studio.** The journal's toolbar gets "Save the journal": a file picker, then every day the disk keeps of the
  source shown, the host and each bot under a heading for "every source"; the lines shown when the host writes no journal.
- **In a report, the owner's choice.** `journal.txt` becomes the character's journal of the last 24 hours read from disk, at
  most its newest 8 MB, else the memory's entries as before; the host's problems of the same 24 hours are a new part, `host.txt`,
  checked by default and unchecked like any other (D410). The names are hidden as before, and so is the player's Windows folder,
  written `%USERPROFILE%`, since the host's paths hold it. The dialog shows a part's last 2,000 lines and says how many more the
  file holds: a day of a journal is tens of thousands of lines, which a text block lays out slowly.
- **No ticket, no capture.** A line is a journal entry's record, made of the game's ids, or a host log message from its template:
  no entry and no warning takes a frame or the login ticket (the relay's messages name connections, sides and sizes), and a test
  plays a connection whose first request presents a ticket and looks for it in every file written.
- **Rejected.**
  - A logging library (Serilog's rolling file): the entries are records the app reads, not log lines, and a sink for them is
    forty lines; a second logging pipeline would also write the host's information lines nobody asked for.
  - Writing from the bot's loop at each entry: the actor's loop must never wait for a disk (D9).
  - One file for every bot: a report and a saved file would have to filter other characters out, names included.
  - Every day kept in a report: up to 175 MB raw for one week of a busy bot, past Discord's limit beside the capture (D410).
  - The host's problems out of the report: a stop at night is as often the relay's or the host's as the bot's, and the part can
    be unchecked.
  - A settings page for the retention now: the captures' has one, but nobody has asked for this one; the keys are enough until then.
