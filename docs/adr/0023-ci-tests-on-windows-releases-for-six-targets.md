# 0023. CI tests on Ubuntu and Windows; releases build six targets through release-please, unsigned for now

- Status: accepted
- Date: 2026-11-23
- Decided by: the owner
- Supersedes 0006

## Context

0006 ran CI on Ubuntu only. The owner plays and measures on Windows, and chose on 2026-11-23 to run the tests there
too on every PR. The rest of 0006 holds and is restated here unchanged.

## Decision

- **CI on Ubuntu and Windows** for PRs: Ubuntu runs every check of `bun run check`; a `Windows` job runs Vitest,
  `cargo test` and the bindings' freshness on `windows-latest`. Its Rust cache is saved from `next` only (about 3 min
  warm, 15 min cold), so PRs' Windows targets do not evict `next`'s within GitHub's 10 GB. The six targets are built
  by the release workflow.
- **Targets**: Windows x64 and ARM64 (NSIS per-user installer and MSI), macOS x64 and ARM64 (two `.dmg`, no universal
  binary), Linux x64 and ARM64 (AppImage, `.deb`, `.rpm`).
- **release-please** keeps a release PR from the Conventional Commits on `next` (changelog, version in `package.json`,
  `Cargo.toml` and `tauri.conf.json`); Claude merges it on the owner's go; the release it creates triggers the build,
  which attaches the installers and the updater's `latest.json`.
- **Versions** start at 0.10.0, above the C# version's 0.9.0; releases are pre-releases until `next` replaces `main`.
- **Unsigned for now**: the README explains SmartScreen's and Gatekeeper's warnings; macOS builds get an ad hoc
  signature. Signing slots into the workflow through secrets later. The updater's own signature (Tauri's key) is
  required from the first release.

## Rejected

- Ubuntu only (0006): a Windows-only test failure would reach the owner's session first.
- Building the three OSes on every PR: slower CI for bugs the release build catches.
- A universal macOS binary: the owner prefers one build per architecture.
- git-cliff with manual tags: more manual steps for the same result.

## Consequences

A Windows-only test failure shows on the PR. `Windows` is not among the rulesets' required checks until the owner adds
it; the `pr` skill waits for it all the same. A platform bug that only a build shows can still reach the release
build; the release is re-run as a patch after the fix.
