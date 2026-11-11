# 0009. The app's identifier is `io.github.aerafaldev.d3b`

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

Tauri's `identifier` names the app to each system: its data and WebView folders, the macOS bundle, the Linux desktop
entry, the single-instance lock. Changing it later leaves every installed copy's data behind and may install the new
version beside the old one instead of over it. v0.10.0 shipped with `dev.aerafal.d3b`, a default nobody chose, and only
the owner has installed it.

## Decision

- **The identifier** is `io.github.aerafaldev.d3b`: reversed from the GitHub organisation that owns the repository,
  which the project can prove it controls, and the form Flathub accepts for an app without a domain of its own.
- **The publisher** is set to `AerafalDev`: without it, Tauri takes the identifier's second part, `github`, as the
  name Windows shows for the publisher.

## Rejected

- Keeping `dev.aerafal.d3b`: no reinstall, but it claims the domain `aerafal.dev`, which the project does not hold, and
  Flathub would refuse it.
- A domain of the owner's: it must then be renewed for as long as the app lives, which the GitHub organisation needs not.

## Consequences

The owner uninstalls v0.10.0 and installs the next release fresh; the first update is measured from that release to
the one after. From now on the identifier and the publisher never change; review refuses a PR that touches either.
