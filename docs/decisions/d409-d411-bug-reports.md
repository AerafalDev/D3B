# Bug reports (D409 to D411)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D409: The game connection is recorded by default, with a retention

*2026-11-09, outside the steps, session "bug reports", chosen by the owner. Builds D37, D53, D61. Refines D37's "off by default".*

The owner collects bug reports from players, and cannot debug a fight, a bank journey or an NPC dialog without the capture of
the session where it happened: a capture must exist when the bug does, and it is replayed by `scripts/fights.cs` and
`scripts/capture.cs`.

- **Measured** on the owner's captures (structure only, D37): one game connection of 6 h 53 min is 19.6 MB, 65,003 records,
  about 3 MB an hour; gzip makes it 3.2 MB, six times smaller. 153 game captures take 241 MB, 1.6 MB on average. A capture
  is small: no rolling buffer is needed, and none would serve, because a replay builds the character, the inventory, the map
  and the fight from the connection's start and cannot begin in its middle.
- **What.** The game connection is recorded by default (`Network:Captures:Enabled` on for the game, in `captures\game`). The
  connection phase is never part of the default: it holds the account's login token (`request.identification.token`) and only
  the mapping tool reads it, so it stays opt-in, as D37 made it.
- **Retention.** The oldest captures are deleted beyond 7 days and beyond 1 GB, the connections still open never; both are
  settings (Settings, Network), applied at each start of the host. With ten accounts playing round the clock the 1 GB holds
  about two days: a report is made soon after the bug, and the numbers are the lead's reckoning, to move once seen in use.
- **Consent.** The first start after the change says on the home page and in the Network settings that sessions are recorded
  on this machine, that nothing leaves it unless the player sends a report (D410), what the retention is and how to turn it off.
- **The ticket.** A capture still holds the account's login ticket on disk until D410's export removes it. It stays local, never
  committed (D24), never shown (CLAUDE.md). Overwriting it as it is written is the better end, left to a later session: it
  touches the relay's writer and nothing needs it yet.
- **Rejected.**
  - On demand: the player turns recording on before playing, so most reports would arrive without a capture, the case this
    decision exists to end.
  - A rolling buffer of the last minutes: a replay needs the whole connection (above). Cutting a long connection to its start
    and its end, with the state between them missing, is premature and unmeasured.
  - Always on without a retention: ten accounts playing round the clock fill about 0.5 GB a day, and tickets stay on disk with no end.

## D410: A bug report is a `.d3breport` bundle, sent by a private Discord webhook

*2026-11-09, outside the steps, session "bug reports", the four choices made by the owner. Builds D409, D37, D2, D24, D112, D275, D349, D383.*

A player reports a bug in the app and the report carries what the maintainer needs to replay it: the capture, the journal and the
trace, the versions and a description. The owner chose, one question at a time, the channel, the recording (D409), what the
bundle holds and how the capture is cleaned, and whether what the bots learned travels with it.

- **A report is one file**, `<code>.d3breport`, a zip of: `manifest.json` (the report's code `D3B-XXXX-XX`, when, the app, client
  build, pack and mapping versions, the area, the character reported as a number with its breed and level and the capture's
  start so that the journal's times join the capture's, and every file with its size and SHA-256); `message.md` (D411); the
  capture of the connection concerned, cleaned; the journal and the trace of that character as text (both are in memory only,
  2,000 entries and 1,000 decisions, D112, D350: a report snapshots them); the profile, routes and zones the character was
  playing and the settings that differ from the defaults; and `knowledge.json` (below). The player's descriptions are free
  text; the area is one of the app's: fights, harvest and bank, moving and maps, NPCs and dialogs, the app.
- **The capture is cleaned at the export.** The report holds a copy: the player's file stays whole. The ticket, and each
  field that names the account, is overwritten by a filler of the same length, so no length and no frame changes and a replay
  reads it as it read the original. Which fields name the account beyond the ticket (the character list and selection) is not
  measured: the first step measures it with `scripts/capture.cs --frames --schema` before the cleaner is written, and a test
  looks for the ticket's bytes in every output. The connection phase's capture, and any capture of another character, never go.
  The characters' names in the journal and the trace are replaced by their number.
- **What the player sees and chooses.** Before anything is sent, a dialog lists each file with its size, shows the journal and
  the trace as text, says what was removed and what the capture still holds (the game's own data: the character's inventory and
  maps, other players' names the client was sent) described by structure, never by content. Each part can be unchecked. For the
  game's areas, fights, harvest and bank, moving and NPCs, the capture is required: the report cannot go without it, and the
  player cancels instead; a report of the app itself has none. Nothing is sent without a click, and the player gets the report's
  code to give on Discord.
- **What the bots learned.** `knowledge.json`, checked by default and removable: the fights' placement cells by map
  (`FightPlacements`, D275) and the NPCs, questions, replies and outcomes (`MapNpcs`, `NpcQuestions`, `NpcOutcomes`, D383), ids
  only, no identity. Merging them into the project's data is a later tool and decision (what enters the pack, who validates),
  not part of the report.
- **The channel is a private Discord webhook**, one multipart post: the bundle as the file and a message with the code, the
  area, the versions and the description's first lines, in a channel only the maintainers read. Discord limits a file's size
  (10 to 20 MB by the sources found, to verify with one real send): a bundle over the limit is not sent, the app saves it, opens
  its folder and gives its code, and the player hands it over on Discord. A whole session of a few hours is a few MB zipped
  (D409), so it fits. The webhook's URL is never in the repository, which is public: the app reads it from a file published as
  a release asset by `scripts/release.cs`, so that it is revoked and replaced without a new app, at the price that anyone who
  downloads the app can read it.
- **What it costs and risks, said plainly.** A webhook cannot rate-limit a player: whoever reads the URL can post spam or files into
  the channel, and the answer is to revoke it; it cannot read anything back. So a report is **untrusted input**: the maintainer's
  tool reads it with bounds (the capture reader's 64 MiB record limit, a zip's entry count and expanded size) and nothing in it
  is ever run. Discord keeps the file until a maintainer deletes the message: reports are deleted once the bug is fixed and at
  the latest after 90 days, which a privacy notice (`PRIVACY.md`, linked from the README and the report dialog) says, with what a report holds and who reads
  it, as players are in the European Union. A scrubbed capture still holds the server's own data: it is never committed (D24),
  never a test fixture; tests build their own captures (D63) from the facts a report shows.
- **For the maintainer**, a tool `scripts/report.cs` unpacks a report under `artifacts/reports/<code>/` (git-ignored), checks
  the hashes and the limits, describes it, and runs `scripts/fights.cs` on its capture with the pack of its client build, which
  the releases (D365) keep.
- **Left, on purpose.** Overwriting the ticket as the relay writes it (D409); a GitHub issue prefilled by the app with the code
  and no capture, for players who have an account there; trimming a long connection; Sentry or an endpoint of our own (below),
  once a report's volume or its abuse proves the webhook too small; a channel for what the bots learned apart from reports.
- **Rejected.**
  - A public issue, pull request or release asset: a capture cannot be public, and a bundle posted there would be read by anyone.
  - A private repository: it needs a credential able to write, which a public app cannot hold.
  - Email: it needs an SMTP secret in the app, or leaves the player to attach the file.
  - A file the player drops on Discord by hand as the only channel: the lead's recommendation for the first step, kept here as
    the fallback; the owner chose the direct send, since reports come often and the friction would lose them.
  - Cloudflare Worker with R2: it gives a retention, a size check and a rate limit, free at this volume, but a second stack
    to host and secure before one report has been read; the owner leaves it for when the webhook is not enough.
  - Sentry: a third party and a tool to learn, with telemetry in a bot that has none; its public key does make abuse a quota
    and not a spam.
  - A capture of only the last minutes, or a report with no capture for the game's areas: see D409 and the owner's demand that
    the capture is required.
  - Sending the capture raw, with only the ticket overwritten: what else of the account the connection holds is not known.

## D411: A report carries an optional message of the player's own

*2026-11-09, outside the steps, session "bug reports", the owner's requirement relayed by the lead session; the limits are the lead's, reversible. Refines D410.*

The owner wants the player to be able to add a message of their own to a report, if they wish. The capture, the area and the
character are what a report needs; the message is not.

- **One free text, optional.** The `description.md` of D410 becomes `message.md`, and a report without one is accepted: the form
  asks for it with a hint, "what you saw and what you expected", and says that nothing else is needed. It is the only free text
  of a report; the area, the character and when stay choices of the form.
- **Limit and language.** 1,500 characters, counted as the player types, never cut silently: the field stops there. Any
  language: the maintainers read French and English, the hint says so, and nothing is translated.
- **Personal data.** The message is the one part no cleaner reads, so the player is told, next to the field and in the
  preview: do not write a password, a login, an email address or a phone number; the message is read by the maintainers and
  kept as long as the rest of the report (D410). Nothing is detected or removed for them: a guess would give a false assurance.
  The message goes to the maintainers as it is typed.
- **Stored** as `message.md` in the bundle, UTF-8, listed in the manifest with its size and SHA-256, and `manifest.json` says
  whether there is one (`hasMessage`). A report is one file, so the message is not a second file beside it.
- **Shown** in the preview with the rest of what goes out, as typed, and unchecked removes it like any part (D410).
- **Sent** inside the webhook's post, not as a message of its own: the post's embed has the code, the area and the versions as
  fields and the whole message as its text (an embed holds 4,096 characters, the message at most 1,500), or "no message". The post
  forbids every mention (`allowed_mentions` empty), so that a message holding `@everyone` pings no one; the file carries it too.
- **Read** by the maintainer first: `scripts/report.cs` prints the message above the description of the capture, with its control
  characters replaced, and unpacks it as `message.md` beside the capture. Like the rest of a report it is untrusted text, never
  run and never rendered as anything but text.
- **Rejected.**
  - A required message: the capture is what a replay needs, and a player who has nothing to add would write filler.
  - A field of `manifest.json` holding the text: the manifest is read by a program, the message by a person, and a text with
    quotes and line breaks in a file is easier to read, to diff and to hash.
  - The message as a Discord message of its own: it could part from its file, and an unmentioned ping is simpler to forbid in one post.
  - Detecting emails or tokens in the text: a rule that misses a case reassures wrongly; the warning and the preview do the job.
  - A limit of 4,000 characters or none: a report's message is read by a person among many, and Discord's own limits would decide for us.
