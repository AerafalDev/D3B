# Privacy

D3B runs on your computer. It has no account, no telemetry and no server of its own. This page says what it records
there, and what leaves your computer if you choose to send a bug report.

## What D3B keeps on your computer

- **Captures of your game sessions**, in `%LOCALAPPDATA%\D3B\captures\game`: the bytes your game client and the game
  server exchanged, so that a bug can be replayed. A capture holds your session's login ticket and what the server told
  your client: your characters, their inventory and maps, and the names of the other players around them. It is kept 7
  days and up to 1 GB by default, then deleted; the Network settings change both and turn the recording off.
- **A database and your settings** in `%LOCALAPPDATA%\D3B`: the accounts and characters D3B saw, what the bots learned, your
  profiles and routes.

None of it leaves your computer by itself.

## What leaves it: a bug report you send

A report leaves only if you open Help, Report a bug, read the preview, save the report and then press **Send**. You may
also save it and hand the file over yourself. The preview lists every part with its size and lets you read and uncheck
each one:

| Part | What it holds |
| --- | --- |
| The capture of the session | Required for a bug of the game: without it the bug cannot be replayed. **Cleaned** first: the login ticket, the other secrets your client sent, and the names of your characters are overwritten. It still holds the game's own data: your character's inventory and maps, the names of other players your client was sent, numbers and dates. |
| Journal and decisions | What your character's bot did, as text; your character is told by its number, not its name. |
| Profile and routes | The profile your character plays and the routes and zones it uses. |
| Settings | The settings you changed from their defaults. |
| What the bots learned | Fight placements and NPC dialogs, by game ids; no name, no account. |
| Your message | Optional, and the only text nothing reads or cleans: **do not write a password, a login, an email address or a phone number.** |

The report also says the app's version, the client build, the area of the bug and your character's number, breed and level.

## Where it goes and who reads it

A report is posted to a private channel of a Discord server that only D3B's maintainers read. Discord is a third party
with its own [privacy policy](https://discord.com/privacy): it stores the file for as long as the maintainers leave it.
The maintainers delete a report once its bug is fixed and at the latest 90 days after they received it.

Pressing Send makes your computer contact two services: GitHub, to read where the channel is, and Discord, to post the report. Both
see your IP address, as any website does; D3B sends them nothing else of you.

Nobody else is meant to see a report, and what is in a report is never published: a capture holds data of Ankama's game
and is never committed to the repository or used in a public test.

## Your choices

- Send nothing: turn the recording off, or never use Report a bug.
- Send less: uncheck any part in the preview, except the capture of a bug of the game.
- Ask for a deletion: give the maintainers the report's code (`D3B-XXXX-XX`, shown when it is saved) on
  [Discord](https://discord.com/invite/6A6Vw9Mn24) or in an issue, and it is deleted.

D3B is not affiliated with Ankama. Questions about this page: open an issue or ask on Discord.
