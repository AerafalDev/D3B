# D3B

An open-source bot for Dofus 3 (Unity), in .NET 10 on Windows. Every feature is a module, assembled as in Bevy. An
Avalonia desktop app runs the bots and controls them; a headless host runs them without it.

This project is not affiliated with Ankama. Dofus is a trademark of Ankama. Using a bot on the official servers breaches
Ankama's terms of service and can get the account banned. No game asset or data is committed to this repository: the
data packs extracted from the official client ship as release assets, and `scripts/extract.cs` makes them from a local
client.

## Status

The solution and the module system: a headless host that builds its modules, starts and stops. The game data: tools that inspect the official client and extract its maps, world graph, items, jobs,
interactives, banks and the icons of resources and professions, with the texts they name, into a pack per client build; a tool that publishes it as a release
asset; and a data module, whose pack the host finds on the machine or downloads, and without which it does not start.
The network: while the host runs, official clients started from the launcher connect through its relay, which can record
their connection phase. The bots walk, travel, use elements and bank. The first milestone is reached: a bot harvests a
profile's resources tour by tour through its zone and banks them when its pods are full, started from the app or when its
client connects, its card and map saying what it does; the profile editor suggests the sub-areas where the resources grow
and draws the zone. The resources and professions show their icons, from the pack, on the profiles, the bots' cards and
the map; every map a bot enters teaches which graphics are which resource, kept from one run to the next, so that a tour
goes first to the maps likely holding its resources. A harvest also takes resources no profession gathers, such as a well's
water, found by those graphics; leaves out a zone's maps reached only far round unless asked; learns how long each
resource takes to grow back and waits for it; tells a protector's fight from an aggression and resumes, pauses or stops
after it; opens resource bags; heals by eating under a share of life; and may deposit in the guild's chest. Each run is
kept, and a statistics page says what the runs brought. Mods will be made as blueprints after it. The app's map page draws a
bot's map from the pack as its character moves, with its path, elements, NPCs and exits, and says what a cell holds;
its logs page shows each bot's journal of its actions and the host's problems; its protocol inspector shows a bot's game
frames decoded as they go, the login ticket hidden, or replays a capture; its settings page holds the client's display
time and D3B's other delays, which a slow machine raises, and the time between two actions, the captures, the language
and the theme. The app was redesigned page by page: a sidebar, one scale of sizes, Lucide's icons, short texts with their help in
tooltips, the profile editor on a page of its own beside its zone's preview, dialogs and toasts. Next: the owner's
harvesting settings, then the app published with its data and the repository public. See the [roadmap](docs/roadmap.md).

## Building

Requires the .NET 10 SDK pinned in `global.json`, on Windows.

```sh
dotnet build
dotnet test
dotnet run --project src/D3B.App
```

`src/D3B.Host` runs the same bots without the app. Both keep the accounts their relay sees and the profiles in
`%LOCALAPPDATA%\D3B\d3b.db` (`Storage:Path` names another file); D3B never sees a password.

The host needs the data pack of the installed client's build. It looks for it in `%LOCALAPPDATA%\D3B\data`, where
`dotnet run --file scripts/extract.cs` writes it, and otherwise downloads it from this repository's release
`client-<version>`. While the repository is private, the download needs a GitHub token: `Releases:Token` in the host's
configuration, or the GitHub CLI logged in with `gh auth login`.

While the host runs, the `ConfigUrl` line of the installed client's `version` file names a copy of the client's
configuration that sends it to the host's relay, and the host gives it Ankama's back when it stops. After a crash,
`dotnet run --file scripts/client.cs -- restore` does it, so that the client plays without D3B.

With `--Network:Captures:Enabled true`, or the switch on the app's settings page, the relay records each connection in
`%LOCALAPPDATA%\D3B\captures`, which the protocol inspector replays. A capture holds the account's login ticket: never
share it.

On a slow machine, the client may not have drawn a map or shown a window when a bot acts: raise the client's display
time on the settings page, 0.5 s by default, up to 3 s. Every other delay is there too, each measured by default and
only raised. The app keeps them in `%LOCALAPPDATA%\D3B\app.json`; `src/D3B.Host` reads the same values from its
configuration, in seconds, such as `--Delays:Display 1.5`.

## Documents

| Document | Content |
| --- | --- |
| [docs/decisions.md](docs/decisions.md) | The brief, every decision with its reasons, the open questions. |
| [docs/architecture.md](docs/architecture.md) | Where the bot is going: projects, modules, runtime, protocol, data, UI. |
| [docs/roadmap.md](docs/roadmap.md) | The steps in order, and the next one. |
| [docs/client.md](docs/client.md) | What the official client holds that D3B uses, measured. |

## License

[MIT](LICENSE). The app's icons are Lucide's, under its own license: see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
