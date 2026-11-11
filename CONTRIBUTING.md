# Contributing to D3B

Thank you for helping. This page covers the setup, how a change gets in, the conventions, and what can never be
committed. The rules for working with Claude Code are in [CLAUDE.md](CLAUDE.md) and `.claude/`.

## Before you start

- **Open an issue first** for anything larger than a small fix, so that we agree on the design before you write it.
  Designs are decided before code here: it is the main lesson of the C# version ([docs/lessons.md](docs/lessons.md)).
- Read [ROADMAP.md](ROADMAP.md) and [docs/now.md](docs/now.md) to see where the work stands, and the
  [decisions in force](docs/adr/README.md) for the area you touch.
- Work happens on branch **`next`**, the rewrite. `main` holds the C# version and only takes critical fixes.

## Setup

You need [Rust](https://rustup.rs), [Bun](https://bun.sh) 1.4 or later, the [.NET 10 SDK](https://dotnet.microsoft.com/download)
(for the game data extractor, which `bun run check` builds and tests), Git, and
[Tauri's system dependencies](https://v2.tauri.app/start/prerequisites/) for your OS.

```sh
git clone https://github.com/AerafalDev/D3B.git
cd D3B
git switch next
bun install --frozen-lockfile
bun run check
bun run app
```

`bun run check` runs everything CI checks and must pass before you open a pull request. The official client is
needed to measure a change in game, not to build or test: tests that need real game data or captures are skipped when
`D3B_DATA` or `D3B_CAPTURES` is not set.

## Making a change

1. Branch from an up-to-date `next`: `git switch -c feat/<subject>`.
2. Write the test first and watch it fail, then make it pass. Every behaviour has a test, and a test of its failure
   case.
3. Keep the pull request to **one behaviour**, within about **600 handwritten lines** (`bun run pr:size`). Larger work
   is a stack of pull requests.
4. Run `bun run check`, then open the pull request against `next` with a
   [Conventional Commits](https://www.conventionalcommits.org) title, such as `feat(relay): hold the server selection`.
   The title becomes the squash commit and the changelog line.
5. CI must be green. Pull requests are squash-merged.

## Conventions

- **Language**: everything in the repository is in English. The app speaks English and French: every visible string
  lives in `src/i18n/locales/en.json` and `fr.json`.
- **Rust** (`src-tauri/`): one crate, one folder per domain; only `ipc/` talks to Tauri. `cargo fmt`, and
  `cargo clippy` with the pedantic lints and no warning. Errors are `thiserror` enums; no `unsafe`.
- **TypeScript** (`src/`): strict TypeScript 7, formatted and linted by Biome; kebab-case file names, one exported
  subject per file, function components, Tailwind classes through `cn()`, state in zustand stores.
- **The IPC is generated**: change the Rust commands and run `cargo test` in `src-tauri/`; never edit
  `src/bindings.ts`.
- **At most 10 code files per folder**, tests apart (`bun run rules`).
- **Comments** say why, not what; public items have doc comments. Workflows carry none.
- **Dependencies**: propose a new one in the issue or pull request, with its reason, size and licence.

## Never commit

- Anything extracted from the official client: game data, assets, bundles, dumps, schemas, mappings. They ship as
  release assets.
- Captures of the game connection: they hold the account's login ticket.
- Secrets of any kind.
- Code copied from other bots, emulators or clients, whatever their licence: read them for ideas only.
- Anything on the [never-list](README.md#what-d3b-never-does). Pull requests adding it are closed.

## Reporting bugs and ideas

Use the issue forms: they ask for what is needed to reproduce a bug (D3B's version, your OS, the client build). Never
attach a capture or a log holding a login ticket. Security issues go through the [security policy](SECURITY.md).

By contributing, you agree that your contribution is released under the project's [MIT license](LICENSE).
