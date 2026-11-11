# Decisions

The decisions in force that are costly to reverse, one ADR each, in the MADR-minimal format of the `adr` skill.
Smaller choices are in their pull requests. An ADR is never rewritten: a later one supersedes it.

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-rewrite-in-rust-tauri-react.md) | Rewrite D3B in Rust, Tauri 2, React 19 and TypeScript, on Bun and Vite | accepted |
| [0002](0002-one-crate-folders-per-domain.md) | One Rust crate, a folder per domain, at most 10 code files per folder | accepted |
| [0003](0003-game-data-as-json-packed-in-the-app.md) | Game data as JSON from a .NET extractor, packed in the app | superseded by 0008 |
| [0004](0004-typed-ipc-with-tauri-specta.md) | A typed IPC generated from Rust with tauri-specta | accepted |
| [0005](0005-how-we-deliver.md) | Claude delivers through squash-merged PRs on protected branches | accepted |
| [0006](0006-releases-for-six-targets.md) | Releases for six targets through release-please, unsigned for now | accepted |
| [0007](0007-the-updater-next-channel.md) | The rewrite updates from an `updater-next` channel, silently, with a restart bar | accepted |
| [0008](0008-world-data-first-in-five-languages-as-a-tar-gz.md) | The data folder holds the world first, in five languages, and ships as a `.tar.gz` | accepted |
| [0009](0009-the-app-identifier-is-io-github-aerafaldev-d3b.md) | The app's identifier is `io.github.aerafaldev.d3b` | accepted |
