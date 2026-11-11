# 0009. The app's identifier is `io.github.aerafaldev.d3b`

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

Tauri's `identifier` is the macOS bundle ID and names the app's data, config and log folders, its WebView profile and
its single-instance lock. Changing it leaves every installed copy's data behind and lets an old and a new copy run at
once. v0.10.0 shipped with `dev.aerafal.d3b`, a default nobody chose, and only the owner has installed it.

## Decision

- **The identifier** is `io.github.aerafaldev.d3b`: reversed from the GitHub organisation that owns the repository,
  which the project controls, the form Flathub uses for apps without a domain of their own.
- **The publisher** is set to `AerafalDev`: without it, Tauri takes the identifier's second part, `github`, as the
  name Windows shows for the publisher and as the Debian package's maintainer.

## Rejected

- Keeping `dev.aerafal.d3b`: nothing left behind, but it claims the domain `aerafal.dev`, which the project does not
  hold.
- A domain of the owner's: it must then be renewed for as long as the app lives, which the GitHub organisation needs
  not.

## Consequences

The installer and the updater key on `productName`, unchanged, so the next release still installs over v0.10.0; its
old folders (`dev.aerafal.d3b`) and its registry key `HKCU\Software\aerafal\D3B` stay behind, which matters to no one
but the owner. From now on the identifier and the publisher never change; review refuses a PR that touches either.
Renaming `productName` would install beside the old copy, and needs its own decision.
