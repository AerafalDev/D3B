# D3B

An open-source bot for Dofus 3 (Unity), in .NET 10 on Windows. Every feature is a module, assembled as in Bevy, and
external .NET plugins add their own. A headless host runs the bots; an Avalonia desktop app controls them.

This project is not affiliated with Ankama. Dofus is a trademark of Ankama. Using a bot on the official servers breaches
Ankama's terms of service and can get the account banned. No game asset or data is part of this repository.

## Status

The solution and the module system: a headless host that builds its modules, loads external plugins from `plugins/`,
starts and stops. No game logic yet. The first milestone is harvesting and banking on one account, controlled from the
desktop app. See the [roadmap](docs/roadmap.md).

## Building

Requires the .NET 10 SDK pinned in `global.json`, on Windows.

```sh
dotnet build
dotnet test
dotnet run --project src/D3B.Host
```

A plugin is a .NET assembly that compiles against the `D3B.Core` package. `templates/d3b-plugin` creates one with
`dotnet new d3b-plugin`; `samples/D3B.Samples.Hello` is a working example.

## Documents

| Document | Content |
| --- | --- |
| [docs/decisions.md](docs/decisions.md) | The brief, every decision with its reasons, the open questions. |
| [docs/architecture.md](docs/architecture.md) | Where the bot is going: projects, modules, runtime, protocol, data, UI. |
| [docs/roadmap.md](docs/roadmap.md) | The steps in order, and the next one. |

## License

[MIT](LICENSE).
