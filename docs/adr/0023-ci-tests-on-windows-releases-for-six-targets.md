# 0023. CI tests on Ubuntu and Windows; releases build six targets through release-please, unsigned for now

- Status: accepted
- Date: 2026-11-23
- Decided by: the owner
- Supersedes 0006

## Context

0006 ran CI on Ubuntu only. The owner plays and measures on Windows, and the audits of 2026-11-23 found nothing on a
PR catching a Windows-only failure before the owner's own session. A full build of the three OSes on every PR stays
too slow for what it would catch.

## Decision

- **CI on Ubuntu and Windows** for PRs: Ubuntu runs every check of `bun run check`; a `Windows` job runs Vitest,
  `cargo test` and the bindings' freshness on `windows-latest`, where the owner plays. Its Rust cache is saved from
  `next` only (about 3 min warm, 15 min cold), so PRs' Windows targets do not evict `next`'s within GitHub's 10 GB. Lints and formatting stay on Ubuntu: they do not change with the OS.
- **Targets**, unchanged: Windows x64 and ARM64 (NSIS per-user installer and MSI), macOS x64 and ARM64 (two `.dmg`, no
  universal binary), Linux x64 and ARM64 (AppImage, `.deb`, `.rpm`), built by the release workflow.
- **release-please**, unchanged: a release PR from the Conventional Commits on `next`, merged by Claude on the owner's
  go; its release triggers the build, which attaches the installers and the updater's `latest.json`.
- **Versions** from 0.10.0, pre-releases until `next` replaces `main`; **unsigned for now** (the README explains the
  warnings, macOS gets an ad hoc signature), the updater's own signature required.

## Rejected

- Ubuntu only (0006): a Windows-only failure reaches the owner's session first.
- Building the three OSes on every PR: slower CI for bugs the release build catches.
- A universal macOS binary; git-cliff with manual tags: as in 0006.

## Consequences

A Windows-only test failure shows on the PR. `Windows` is not among the rulesets' required checks until the owner adds
it; the `pr` skill waits for it all the same. A platform bug that only a build shows still reaches the release build.
