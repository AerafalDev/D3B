# 0006. Releases for six targets through release-please, unsigned for now

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

The app must install and update itself on Windows, macOS and Linux, on x64 and ARM64, from this repository's
releases, without slowing every PR's CI.

## Decision

- **CI on Ubuntu only** for PRs; the six targets are built by the release workflow.
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

- Building the three OSes on every PR: slower CI for bugs the release build catches.
- A universal macOS binary: the owner prefers one build per architecture.
- git-cliff with manual tags: more manual steps for the same result.

## Consequences

A platform bug can reach the release build before anyone sees it; the release is re-run as a patch after the fix.
