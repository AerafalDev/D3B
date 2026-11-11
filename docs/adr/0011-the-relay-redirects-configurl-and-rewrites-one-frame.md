# 0011. The relay redirects the client's `ConfigUrl` for the app's life and rewrites one frame by field path

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

Step 2 observes the game, so every connection of the client must pass through D3B. The C# version measured a way that
needs no elevation, no injection and no certificate: the client's `version` file names its configuration URL, and a
`file:` copy whose connection host is on loopback is followed (`main:docs/client.md`, D35). The game server's address
then comes in one frame of the connection phase, which must change for the game connection to reach the relay too.

## Decision

- **The redirect** is the C# one: the `ConfigUrl` line points at a `file:` copy of Ankama's JSON with the connection
  host replaced by `127.0.13.59`; listeners on ports 5555 and 443 (connection phase) and 5556 (game), fixed. A port
  that cannot be taken stops the redirect with the port named.
- **For the app's life**: applied at start, put back at quit and before an update installs, only if the line is still
  D3B's; `version` polled every 2 s to apply again after the launcher rewrites it. No switch in the interface.
- **The state** (the original URL, the last JSON from Ankama, the copy) lives in the app's local data folder, outside
  the install folder.
- **One frame rewritten**, the server-selection answer (path 2.4.1, `host = 2`, `ports = 3`), by protobuf field path
  with our own wire walker, only when it is read whole and encodes back to its own bytes; anything else passes
  untouched. The game connection is routed by the ticket found by wire format alone. No game mapping, so any build.
- **Tokio and reqwest** become direct dependencies (already in the lockfile through Tauri and its updater).
- **Windows only** until a tester measures macOS and Linux; there the relay says it is not measured and redirects
  nothing.

## Rejected

- An explicit "Start relay" switch: more states to show and test, for no need measured yet.
- Prost structs for the connection messages now: settles the protocol feature's library before it is designed.
- Waiting for the protocol feature: the game connection would not reach the relay, nothing could be measured.
- `notify` for the watcher: a crate to watch one 113-byte file. A free port picked by the OS: not measured.
- Keeping the state beside `version`: an update or a repair of Ankama's may delete or flag it.

## Consequences

A client started before D3B is not relayed; one started after D3B crashed cannot connect until D3B starts again or
the launcher repairs. Two D3B instances would share the one `version` file: the single-instance plugin keeps one per
user. Each client update is measured again, since Ankama could refuse `file:` or check the file.
