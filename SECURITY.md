# Security policy

## Supported versions

D3B updates itself to its latest release, and only that release gets security fixes.

| Version | Supported |
| --- | --- |
| The latest release | Yes |
| Older releases | No: update from the app |
| Branch `next`, the rewrite | Fixed there first |
| The C# version on `main` | Critical fixes only, until the rewrite replaces it |

## Reporting a vulnerability

**Do not report a vulnerability in a public issue, pull request, comment or Discord channel.**

Report it privately through GitHub: on the repository's [Security](https://github.com/AerafalDev/D3B/security) tab,
choose *Report a vulnerability*. Only the maintainer sees the report, and the fix is discussed with you there.

Please include:

- what an attacker can do, and what they need first (running a program on the same machine, sending a crafted file,
  controlling a network);
- the steps to reproduce it, with the smallest input that shows it;
- D3B's version, your OS and architecture, and the client build, all shown in the app;
- whether you want to be credited, and under which name.

Never include a real capture, database or login ticket. If the issue needs one, describe its structure, or make one on
an account you can afford to lose.

## What matters most in D3B

D3B relays a game client's connection, keeps data about accounts, and updates itself. Reports about these are the most
useful:

- **The relay.** It listens on loopback and forwards the client's connection to Ankama's servers. Anything that makes
  it reachable from another machine, sends the client or its traffic elsewhere, or lets another program inject frames
  into a bot's connection.
- **Login tickets.** The relay sees the account's login ticket. Any path through which a ticket leaks into a log, the
  interface, an error message or a file.
- **The client's configuration.** While D3B runs, it rewrites one line of the client's `version` file and writes a copy
  of Ankama's configuration. Anything that lets another party choose what that copy says, or leaves the client pointed
  somewhere unexpected once D3B stops.
- **Untrusted input.** Frames from the server or the client, captures, and game data files. A crafted input that
  crashes or hangs the app, or makes it read or write outside what it should.
- **The webview.** Anything that runs remote code in the interface, escapes its content security policy, or reaches a
  back-end command it should not.
- **Updates and releases.** The app installs updates signed with the project's updater key, from this repository's
  releases. Anything that lets someone other than the maintainer publish an update an installed app would take.

## Out of scope

- **Ankama's game, client, servers and protections.** Report those to Ankama.
- **Getting banned.** Using a bot breaches Ankama's terms of use, and D3B does nothing to hide it. Ways for Ankama to
  detect D3B are not vulnerabilities.
- **An attacker who already runs code as your user.** Such a program can read and change everything D3B keeps.
- **The unsigned installers.** The operating systems warn about them, as the README says.

## What happens next

D3B is maintained by one person in their free time. You can expect an acknowledgment within a week; then an
assessment, and a fix or a decision not to fix with its reasons, discussed with you in the private report; then a
release with the fix, and the advisory published, crediting you unless you prefer otherwise. Please allow a
reasonable time to ship the fix before disclosing the issue publicly.
