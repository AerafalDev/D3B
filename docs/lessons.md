# Lessons from the C# version

The C# version (`main`, .NET 10 and Avalonia, Windows only) reached harvesting, banking and AI fights in 491 commits
over two weeks. What it did well is kept; what went wrong is the reason for most of the rules in `CLAUDE.md`.

## What went wrong

- **Designs changed after they were coded.** Plugins, then Lua, then blueprints; a gRPC host, then in-process; the
  studio shell built (7c to 7c4) then redesigned (7r) because it was impractical. → Every design is decided with the
  owner first, on a mockup for an interface, and nothing abstract is built without two real users.
- **Features piled up unmeasured.** About fifteen were tested only on a fake game or in headless renders. → A feature is
  done only when the owner measured it in game; the next one waits.
- **Process outgrew the code.** 423 decisions, 1.9 MB of docs, about 400 KB to read before each session, docs drifting
  from the code. → ADRs only for decisions costly to reverse, one short page per domain, `docs/now.md` under a page.
- **Profiles, routes and zones**: too many concepts, tedious to create, rigid, opaque. → Step 3 designs the bot's
  model from scratch.
- **Heavy tooling**: 900 lines of C# hooks, a 17 s build at every turn's end, a CI listing every script by hand. → A
  few short hooks, permissions for hard rules, CI that discovers what it checks.
- **Windows only**: hard-coded `%LOCALAPPDATA%` paths, `StandaloneWindows64` bundles, releases for `win-x64`. → Paths
  through Tauri, six release targets, platform code behind `cfg`.

## What is kept

- The never-list (no detection evasion, no mass accounts, no payment automation, no Shield bypass, no proxy per
  account) and "nothing from the client is committed".
- The relay: the client's `version` file points its `ConfigUrl` at a copy of Ankama's configuration whose connection
  host is the relay on loopback; no injection, no password, restored on stop.
- A readable protocol over an audited per-build mapping; frames not understood pass through; an unknown build stops
  the bot.
- One actor per character over immutable state snapshots, injected time, cancellation and recovery.
- Tests on captures replayed through the relay, property tests for paths and line of sight, a golden-fight bench.
- One fight AI driven by the effects as data, not per-breed scripts.
- The measured references on `main`: `docs/client.md` and `docs/client-game.md`, read through `study`.
