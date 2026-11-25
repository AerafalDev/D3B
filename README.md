# D3B

[![CI](https://github.com/AerafalDev/D3B/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/AerafalDev/D3B/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/AerafalDev/D3B?label=release)](https://github.com/AerafalDev/D3B/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Discord](https://img.shields.io/badge/Discord-join-5865F2?logo=discord&logoColor=white)](https://discord.com/invite/6A6Vw9Mn24)

An open-source bot for Dofus 3, on Windows. D3B plays your characters through the official client: it harvests, hunts
and fights, alone or in a group, and a desktop app shows what each bot does.

> [!WARNING]
> D3B is not affiliated with Ankama. Using a bot on the official servers breaches Ankama's terms of use and can get your
> account banned. You use D3B at your own risk.

## Features

- **Harvest**: tours the zones that suit your level and banks what it gathers. Ready-made profiles for each profession.
- **Hunt**: attacks the groups of monsters your profile allows, only those it expects to beat; to farm anomaly fragments, it
  can keep a Minor Uchronic Elixir going; with a capture net worn, it can capture wild Dragodindes, Muldos and Volkornes.
- **Dungeons**: runs the dungeons your profile lists, in turn, from the keeper to the exit, with the keyring or the keys
  carried; it banks first when the next run would fill the character.
- **Fight AI**: plays every class, and the fight's easy challenges.
- **Groups**: one character leads, the others follow it, fight beside it and bank with it.
- **Bank and guild chest**: moves items between your bank and your guild's chest, either way: everything, or by category,
  type or item, with exclusions and quantities, in as many trips as your pods need. A dry run says what would move first.
- **Studio**: your characters live, profile and route editors, a journal per bot, kept on disk for a week and saved in one click. In French, English and Spanish.

D3B injects nothing into the game and never sees your password.

## Install

1. Install Dofus 3 with Ankama's launcher, on Windows 10 or 11.
2. Download and run `D3B.App-win-Setup.exe` from the [latest release](https://github.com/AerafalDev/D3B/releases/latest).
   No administrator rights are needed. If Windows SmartScreen stops it, choose *More info*, then *Run anyway*.

D3B finds the game where Ankama's launcher installed it, on any drive. If it does not, choose the game's folder, the one
that holds `Dofus.exe`, in the settings (Files, *Game folder*), then restart D3B.

D3B updates itself. After an update of the game, wait for the D3B version that supports it.

## Getting started

1. Start D3B, and once its status bar says *Relay ready*, start Dofus and choose a character. It shows in D3B's **Characters**, the first entry of the bar
   on the left.
2. Double-click it in Characters: its tab opens, and the column on its left chooses its profile and plays it (or press
   F5).
3. To take the character back, just play it: the bot pauses until you press Play again.

Ctrl K finds a profile, a route, a character or a map; Ctrl Shift P lists every command, a character's Play and Stop among them.
D3B opens again as you left it: its tabs, its panels and their sizes. An empty view says what fills it: with no character
connected, Characters lists the steps above. Characters lists those you played through D3B; its ▶ plays every one connected.

For a group, make a **Follow** profile for the followers (*Add* in the **Library**, the bar's second entry) and name their leader in it. The
leader plays any profile. When every account is subscribed and the followers are level 10 or more, they follow, join
the fights and get ready through the game's own party options, and D3B still watches that none is left behind.

If D3B crashes, the game cannot connect until you start D3B again and close it.

## Help

- **A bug in the game?** Use *Help → Report a bug* in the app. You see what the report holds before sending it
  ([privacy](PRIVACY.md)).
- **A question?** Join the [Discord](https://discord.com/invite/6A6Vw9Mn24) or open an
  [issue](https://github.com/AerafalDev/D3B/issues/new/choose).

## What D3B never does

No evading Ankama's detection, no mass account creation, no subscription or payment automation, no bypassing Ankama
Shield, no proxy per account to hide multi-accounting
([D2](docs/decisions/d001-d018-foundation.md#d2-what-d3b-never-contains)). Pull requests that add one are closed.

## Development

D3B is written in C# on .NET 10.

```powershell
git clone https://github.com/AerafalDev/D3B.git
cd D3B
dotnet build                          # warnings are errors
dotnet test                           # every test project, on Microsoft.Testing.Platform
dotnet format --verify-no-changes     # CI checks it
dotnet run --project src/D3B.App      # the desktop app, which runs the bots' host in its process
dotnet run --project src/D3B.Host     # the same host without the app, until Ctrl+C
```

- [docs/development.md](docs/development.md): running from source, the host's options, the tools, packs and releases,
  how D3B works.
- [CONTRIBUTING.md](CONTRIBUTING.md): the conventions and the checks before a pull request.
- [docs/status.md](docs/status.md) and [docs/roadmap.md](docs/roadmap.md): where the project stands and what comes next.

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md). Report a vulnerability through the
[security policy](SECURITY.md), never a public issue.

## License

[MIT](LICENSE). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
