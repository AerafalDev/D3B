# 0012. The protocol decodes with prost over our own `.proto` files, paired with each build by committed pins

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

Step 2 reads the game connection, whose messages bear obfuscated three-letter names that may change at each patch
build (every one to two weeks; whether they reshuffle names and numbers is not measured, the C# version's Q17). The C#
version paired them by a matcher of pins, capture anchors and structural scores, whose mapping format changed 16 times.

## Decision

- **Decoding**: `prost` over readable `.proto` files we write, compiled by `prost-build` with `protox` (no `protoc`).
  It will also encode the requests of step 4.
- **Pins per build**, committed in `src-tauri/protocol/pins/<build>.json`: our readable name to the build's name, for
  the messages a feature reads only, the build's session-secret messages, and the digest of its schema. They are ours.
- **The schema** is read from the client's metadata by the .NET extractor (Cpp2IL, as ADR 0010) into `artifacts/`,
  never shipped; an audit test checks the pins against it before they are committed.
- **The gate**: a connection whose build (path 1.3.5 of its identification) has pins is decoded; any other is relayed
  untouched with the protocol off and its reason shown.
- **Captures** through `D3B_RECORD=<folder>`, game connections only, with no interface.

## Rejected

- The schema shipped in `data/`, audited at start: data format 2 and a new 3.7.13.17 asset, for the audit's check.
- Pins checked by a capture only: two messages of one shape (37 of the 63 the first milestone needs) could swap.
- The C# matcher and mapping format: built before Q17 is measured.
- rust-protobuf, hand-written decoders, prost-reflect: less common, linear in messages, or stringly typed.
- A capture toggle in the interface, or the connection phase recorded: shares the ticket or the login token too easily.

## Consequences

A new build is decoded once its pins are released; until then it plays through the relay, protocol off. If Q17 shows
reshuffling builds, each needs new pins and a matcher is designed on that measure. The protocol inspector shows an
unpinned message only by field numbers and wire types (`relay::wire`), since no schema ships.
