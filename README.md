# D3B

An open-source bot for Dofus 3 (Unity), in .NET 10 on Windows. Every feature is a module, assembled as in Bevy. An
Avalonia desktop app runs the bots and controls them; a headless host runs them without it.

This project is not affiliated with Ankama. Dofus is a trademark of Ankama. Using a bot on the official servers breaches
Ankama's terms of service and can get the account banned. No game asset or data is committed to this repository: the
data packs extracted from the official client ship as release assets, and `scripts/extract.cs` makes them from a local
client.

## Status

The solution and the module system: a headless host that builds its modules, starts and stops. The game data: tools that inspect the official client and extract its maps, world graph, items, jobs,
interactives and banks, with the texts they name, into a pack per client build; a tool that publishes it as a release
asset; and a data module, whose pack the host finds on the machine or downloads, and without which it does not start.
The network: while the host runs, official clients started from the launcher connect through its relay, which can record
their connection phase. The bots walk, travel, use elements and bank, as activities the app starts, which D3B's
modules register from the first milestone on; mods will be made as blueprints after it. The app's logs page shows each
bot's journal of its actions and the host's problems; its protocol inspector shows a bot's game frames decoded as they
go, the login ticket hidden, or replays a capture. The first milestone is harvesting and banking on one account,
controlled from the desktop app. See the [roadmap](docs/roadmap.md).

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

With `--Network:Captures:Enabled true`, the relay records each connection in `%LOCALAPPDATA%\D3B\captures`, which the
protocol inspector replays. A capture holds the account's login ticket: never share it.

## Documents

| Document | Content |
| --- | --- |
| [docs/decisions.md](docs/decisions.md) | The brief, every decision with its reasons, the open questions. |
| [docs/architecture.md](docs/architecture.md) | Where the bot is going: projects, modules, runtime, protocol, data, UI. |
| [docs/roadmap.md](docs/roadmap.md) | The steps in order, and the next one. |
| [docs/client.md](docs/client.md) | What the official client holds that D3B uses, measured. |

## License

[MIT](LICENSE).
