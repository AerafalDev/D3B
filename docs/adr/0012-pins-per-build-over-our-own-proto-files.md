# 0012. The protocol decodes with prost over our own `.proto` files, paired with each build by committed pins

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

Step 2 reads the game connection. Its messages are named by the client's obfuscated three-letter names, which may
change at each build (patch builds come every one to two weeks; whether they reshuffle names and field numbers is not
measured, the C# version's Q17). The C# version paired them with its readable protocol by a matcher of pins, capture
anchors and structural scores, whose mapping format changed 16 times (`main:docs/client-game.md`, D42 to D59).

## Decision

- **Decoding**: `prost`, over readable `.proto` files we write, compiled at build time by `prost-build` with `protox`
  (no `protoc` to install). It will also encode the requests of step 4.
- **Pins per build**, committed in `src-tauri/protocol/pins/<build>.json`: our readable name to the build's
  three-letter name, for the messages a feature reads only, with the digest of the build's schema. They are ours, not
  extracted data. Session-secret messages are never pinned.
- **The schema** is read from the client's metadata by the .NET extractor (Cpp2IL, as ADR 0010) into `artifacts/`,
  never shipped; an audit test checks the pins against it before they are committed.
- **The gate**: a connection's build (path 1.3.5 of its identification) with pins is decoded; any other is relayed
  untouched with the protocol off and its reason shown.
- **Captures** are recorded through `D3B_RECORD=<folder>`, game connections only, with no interface.

## Rejected

- Shipping the schema in `data/` and auditing at start: data format 2 and a new asset for 3.7.13.17, for a runtime
  check the maintainer's audit already makes.
- Pins checked by a capture only: two messages of one shape (37 of the 63 the first milestone needs) could swap.
- The C# matcher: more than 2,000 lines built before Q17 is measured.
- rust-protobuf, hand-written decoders, prost-reflect over the client's schema: less common, linear in messages, or
  stringly typed with the schema shipped.
- A capture toggle in the interface, or the connection phase recorded: either shares the login token or the ticket
  too easily.

## Consequences

A new build is decoded only once its pins are committed and released: until then its clients play through the relay
with the protocol off. If Q17 shows builds that reshuffle names, each needs new pins (the audit says which); a matcher
is designed then, on that measure. The readable `.proto` files grow one message at a time, with the feature that reads
it.
