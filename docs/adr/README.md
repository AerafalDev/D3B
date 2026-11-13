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
| [0008](0008-world-data-first-in-five-languages-as-a-tar-gz.md) | The data folder holds the world first, in five languages, and ships as a `.tar.gz` | superseded by 0013 |
| [0009](0009-the-app-identifier-is-io-github-aerafaldev-d3b.md) | The app's identifier is `io.github.aerafaldev.d3b` | accepted |
| [0010](0010-flags-by-the-names-of-the-clients-enums.md) | Flags are written by the names the client's enums give them, read with Cpp2IL | accepted |
| [0011](0011-the-relay-redirects-configurl-and-rewrites-one-frame.md) | The relay redirects the client's `ConfigUrl` for the app's life and rewrites one frame by field path | accepted |
| [0012](0012-pins-per-build-over-our-own-proto-files.md) | The protocol decodes with prost over our own `.proto` files, paired with each build by committed pins | accepted |
| [0013](0013-the-bot-runs-unreal-like-blueprints-set-per-character.md) | The bot runs Unreal-like blueprints, set per character | accepted |
| [0014](0014-the-players-files-are-json-in-documents-d3b.md) | The player's files are one JSON per object in Documents/D3B | accepted |
| [0013](0013-a-new-data-format-is-a-second-asset-named-by-its-format.md) | A new data format is a second asset of the build's release, named by its format | accepted |
