# 0007. The rewrite updates from an `updater-next` channel, silently, with a restart bar

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

The rewrite publishes pre-releases until it replaces the C# version (ADR 0006), and GitHub's `releases/latest` ignores
pre-releases, so Tauri's usual endpoint would never see an update. The endpoint is built into every binary shipped:
changing it later reaches only the copies that already updated.

## Decision

- **The endpoint** is `releases/download/updater-next/latest.json`: a fixed pre-release, `updater-next`, whose
  `latest.json` the release workflow overwrites once all six targets are built and listed in it.
- **The behaviour**: at start, outside development, the app checks and downloads a newer version in the background,
  then shows a bar offering to restart into it; if the player does not, the update installs as the app closes, without
  relaunching. Updates are signed with the project's updater key, which the owner holds.
- **At the switch**, the release that moves the endpoint to `releases/latest/download/latest.json` is still published
  on `updater-next`; the channel goes only in the release after.

## Rejected

- No updates before the switch: every test build reinstalled by hand.
- A stable and beta channel chosen in the app: more work now; it can supersede this one later.
- Asking before downloading, or a manual check only: more friction than the C# version had.

## Consequences

Testers get every pre-release automatically. Forgetting the switch's order strands installed copies on the old
channel for good.
