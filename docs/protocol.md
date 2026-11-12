# The protocol

D3B reads the game connection's frames as readable messages: the relay splits the game connection into frames while
it copies every byte untouched, and the protocol decodes the frames of a build it has pins for. A build without pins
is relayed as before, with the protocol off for that connection
(ADR [0012](adr/0012-pins-per-build-over-our-own-proto-files.md)).

```mermaid
flowchart LR
    cp["relay: connection phase, client_version at 1.3.5"] -->|"build, with the ticket"| ho[handover]
    ho --> gc["relay: game connection, two pumps"]
    gc -->|"bytes as read, untouched"| net["client and game server"]
    gc -->|"each whole frame, try_send"| ch[("bounded channel per connection")]
    gc -.->|"D3B_RECORD"| rec["relay::capture, .d3bcap format 2"]
    ch --> pt["protocol: envelope, Any, pins of the build"]
    pins["src-tauri/protocol/: our .proto files, pins/BUILD.json"] --> pt
    pt -->|"counts and the map, at most 1/s"| st["RelayStatus, per connection"]
    st --> ipc["ipc: RelayChanged"] --> bar["status bar tooltip"]
    ext["extractor: schema from the metadata, artifacts/protocol/BUILD.json"] -.->|"audit test, maintainer"| pins
```

The facts below come from the study `artifacts/studies/protocol.md` (the owner's 225 captures, the installed client
3.7.13.17 read with Cpp2IL, and the C# version's measured docs `main:docs/client.md` and `main:docs/client-game.md`).
Each is `inferred` unless said `verified`.

## On the wire

- **Framing** (verified, 54 game connections of 3.7.13.17): every byte, both ways, is in varint-length frames, as in
  the connection phase; no TLS, no compression. The largest frame is 88,581 bytes; 4.6% of the server's reads end
  inside a frame, so frames are reassembled across reads.
- **The envelope** (verified): a client frame is the root's field 4, an `int32` then a `google.protobuf.Any`; a server
  frame is the root's field 3 (an event, an `Any`) or field 1 (a response, an `int32` then an `Any`). The root's field
  2 and the event's list are never set. Every type URL is `type.ankama.com/` and a type's three-letter name.
- **The names** are obfuscated: three lowercase letters for a type, and the members' names too. The type URL names the
  message, so pins pair our readable names with these names, build by build.
- **The build** of each connection (verified, 171 connection phases): the identification request carries the client's
  version at path 1.3.5, equal to the build each capture recorded. No game frame holds it, so the relay reads it in the
  connection phase and hands it over with the ticket.
- **Between builds**: 3.7.13.17's schema is 3.7.12.12's byte for byte (the C# version, D361); what a build that changes
  the schema does is not measured (the C# version's Q17). Patch builds come every one to two weeks.

## The tap

- The game connection is copied by two pumps, one per direction, each writing the bytes it read before anything else,
  then feeding them to a frame reader (`relay::frame::Frames`). Each whole frame goes to the connection's protocol task
  through a bounded channel (about 1,024 frames) with `try_send`: when it is full the frame is dropped and counted,
  never waited for, so the protocol can never slow the game.
- A framing error (a sixth length byte, a frame over 16 MiB) stops the tap for that connection, whose protocol is then
  **lost**; the copy goes on. `protocol::session` is that task: the relay's `game_connection::serve` opens it once a
  ticket is taken, with the build the handover carried; its gate reads a build with pins and leaves any other unread; it
  counts each frame's outcome, keeps the map of the last `MapCurrentEvent`, and tells the relay's status at once, then
  at most once a second while something changed, and once more when the connection ends, which removes it. Half-close
  and errors behave as with `copy_bidirectional` today.
- A dropped frame can leave the state stale (a dropped current map keeps the last one shown until the next): the
  status shows the count, and the live view says so ([game.md](game.md#the-state-of-a-character)).
- `src-tauri/src/relay/` already holds 10 code files: the frame reader goes into `frame.rs`, the pumps into
  `game_connection.rs`, the build into `connection_phase.rs` and `handover.rs`, and captures into a new
  `relay/capture/` (the reader moved out of `tests.rs`, and the writer).

## Decoding

- **Our readable `.proto` files** in `src-tauri/protocol/game/` (`character.proto`, `maps.proto`, package `d3b.game`),
  written by us with the field numbers checked by hand against 3.7.13.17's schema, then by the audit test, compiled with
  `prost` by `prost-build` and `protox` in `build.rs` (no `protoc` to install) into `protocol::game`;
  `protocol::message::Message` holds the live view's messages by their readable names. Names are the C# version's
  (inferred); a message declares only the fields D3B reads (partial, so its frames are "read"), an enum it does not need
  is read as its number, and no map is declared (prost's maps would not keep the client's order).
- **Pins**, `src-tauri/protocol/pins/<build>.json`: `{build, schemaSha256, messages: {Readable: "abc"}, secrets:
  ["abc"]}`, one entry per message a feature reads, embedded in the app. `secrets` names the build's messages that
  carry a session secret (the ticket's request, and those the study found holding secrets): their frames are dropped
  before any decoding, and a name both pinned and secret fails the pins' check. `protocol::pins` checks a file as it
  reads it: its build, a lowercase SHA-256, readable names among `protocol::message`'s, names of 1 to 8 lowercase
  letters, none pinned twice. 3.7.13.17's pin the live view's 15 messages, the pairs the C# version measured by play
  (inferred, `main:src/D3B.Protocol/Pins/3.7.13.17.json`), and 6 secrets: the ticket's request (verified, the 54 game
  connections' first frames), the 4 the study found holding session secrets (inferred), and `koh`, whose 231 frames
  each carry a distinct hexadecimal secret (verified, the live view's study). The live view adds 2 pins,
  `CharacterLevelUpEvent` and `KamasUpdateEvent` ([game.md](game.md)). `protocol::decoder` reads
  one frame with them: its envelope, a secret left undecoded, a pinned name decoded, then its outcome.
- **The gate**: a connection whose build has pins is decoded; one without (**unmapped**), or with no build read
  (**unknown build**), has the protocol off and the reason in its status. Nothing else changes for it.
- **Four outcomes** per frame (`.claude/rules/game-data.md`: a mapped message that does not re-encode to the same
  bytes is not trusted). prost 0.14 writes a oneof where its lowest field number would be, whichever member is set
  (verified, 2026-11-12, `protocol::message`'s tests), and drops unknown fields, where the client writes fields in
  number order, so the frame and its re-encoding are compared field by field in number order, read by
  `relay::wire`:
  - **understood**: decoded, and the same fields with the same bytes;
  - **read**: decoded, the same but for fields our `.proto` lacks, present in the frame only;
  - **unknown**: a name without a pin, or an envelope not as above, passed on and counted;
  - **not trusted**: a pinned name whose bytes do not decode, or a declared field that re-encodes otherwise.

  `protocol::envelope` opens a frame by the wire format alone, with no allocation: exactly the fields above, an id of 0
  left out, an empty message's `Any` without its value, a name of 1 to 8 lowercase letters (3.7.13.17's are 3).
  `protocol::outcome` compares down to 16 levels of nested messages (the live view's deepest is 5) and 131,072 fields in
  all (4 MiB; the largest frame measured holds under 45,000); a repeated field keeps its count and order, and a field
  prost wrote that the frame lacks is not trusted. With no schema at hand, "read" also covers a field our `.proto`
  declares without the presence the build gives it, sent at 0, and a oneof sent twice: the audit test checks that our
  `.proto` keeps the build's `optional` and oneofs. A secret message's frames count as unknown.
- **To start**, the messages the live view needs: the character chosen, the current map and its information (and its
  request), the context created and destroyed, actors shown, removed and moving, interactive and stated elements
  updated, the characteristics, life regeneration, the weight, a level gained and the kamas.

## A new build

1. The maintainer runs the extractor on the new client: it reads the protocol's schema from the IL2CPP metadata with
   Cpp2IL (messages, field numbers, types, oneofs; no machine-code pass) and writes
   `artifacts/protocol/<build>.json`, with a `format` and the SHA-256 of its canonical form (keys and messages sorted).
   Nothing of it is committed or shipped. A change of that format updates every pinned build's `schemaSha256` in the
   same PR. The same command writes the data folder (the owner, 2026-11-12). Format 1 (`tools/extractor/src/Protocol/`)
   lists the game connection's messages by name, a nested one after its declaring types with dots, each field's
   number, kind (`int32`, `int64`, `uint32`, `bool`, `string`, `float`, `enum`, `message`, `map` on 3.7.13.17), its
   type, `repeated` or `optional`, and its oneof; and the enums the fields hold, with their values. The canonical form
   is compact and omits the build, so two builds of one schema share their digest. The metadata gives C# types: an
   `int` is written `int32` though `sint32` and `sfixed32` are `int` too (none on 3.7.12.12, the C# version's D40); a
   decode that does not re-encode the same bytes shows one. Measured on 3.7.13.17 (verified, 2026-11-12): 2,175
   messages, 6,302 fields (401 `optional`, 770 `repeated`, 201 maps, 349 in oneofs), 422 enums, in the same run as
   the data; two runs write the same bytes.
2. When the digest equals a pinned build's, its pins are copied under the new build. Otherwise the pins are made again
   and the audit test (ignored unless that schema exists) checks that every pinned message and field exists with the
   same number and kind and that `schemaSha256` matches; a capture of the new build replayed must show no frame
   "not trusted".
3. The pins are committed; the next release decodes that build.

## Captures

`D3B_RECORD=<folder>` records each game connection to `<folder>/<build>-<Unix milliseconds>-<n>.d3bcap`, in the C#
version's format 2, which the replay reads (`relay::capture`): each read of each side as it came, the client's first
ones as one record up to its first frame. Nothing is written for a game server that cannot be reached. The build is the
connection's own, from its connection phase, or `unknown` when none was read. Game connections only: the connection
phase holds the launcher's login token. Each read is handed to a blocking writer without waiting, at most 1,024 waiting;
one more cuts the capture, and so does a write that fails: nothing more is recorded, the file ends at its last whole
record and its name ends in `-cut`. A folder that cannot be made, or a name already taken, gives no capture, and the
game goes on. `D3B_RECORD` is taken only as an absolute folder. No interface: a capture holds the account's ticket and
is never shared. It is the one exception to the relay's secrets rule ([relay.md](relay.md#secrets)).

## In the app

`RelayStatus.connections` holds one entry per game connection: its build, its protocol state (on, unmapped, unknown
build, lost), updated at most once a second, the counts of the four outcomes and of dropped frames (saturating at
`u32::MAX`), and its current map's coordinates from the game data when the data's build is the connection's: only
these prove a name was paired with the right message. The relay's tooltip (`src/relay/`) shows each connection on two
lines, the owner's choice (2026-11-12): who it is (its number, build, protocol and map), then, when it is read or
lost, its counts; one line when it is not read. No new command.

## The inspector

The owner's choices of 2026-11-12, on the mockup: a character's **Protocol** tab ([game.md](game.md#in-the-app)) lists
its connection's frames live, newest last, and shows the one clicked.

- **Kept**: each connection's protocol task keeps its last 3,000 frames and 16 MiB at most, whichever comes first,
  from its start, freed at its close (`protocol::session::frames`): each with its number, time, side, name, readable
  name, outcome and size. A secret's frame keeps its name and size only, never its bytes. A frame the tap dropped is
  not there; the count of dropped frames is.
- **Streamed only while watched**: `inspector_watch(connection, true)` answers with the kept frames' summaries and
  starts `InspectorFrames`, at most ten batches a second of at most 1,000 summaries each, with the count of those
  skipped beyond (the peak measured is 737 frames a second, 93 KB, on one connection); `false`, the tab closed or the
  connection closed stops it. `inspector_frame(connection, number)` answers one frame's detail, or that it is gone.
- **A frame's detail**: a pinned message decoded, its fields by their names (players' names as the game shows them);
  under it, the fields our `.proto` does not read. Any other frame, by the wire walker (`relay::wire`) alone: field
  numbers, wire types, varints and lengths. **Text is never shown**, only its length: an unpinned message may hold
  the chat, the account's nickname or names, and no list of them is kept per build. No bytes, no export, no copy.
- **The list** shows the visible rows only (fixed row height, by hand), filters by side and by name, and pauses: the
  list stops, a counter shows what arrived meanwhile.

## Tests

Unit tests per file with their failure cases, on synthetic frames: the frame reader (a header split across reads,
several frames per read, an 88,581-byte frame over 11 reads; a sixth length byte, a frame over the limit), the pumps
(bytes out equal bytes in with the channel full), the build at 1.3.5, the envelope, the pins' checks (a name pinned
twice, a readable name the `.proto` lacks, a secret message pinned), each message, the capture writer read back. The
capture replay (`D3B_CAPTURES`) checks every frame is passed on, none mapped is "not trusted", and the live view's
messages are decoded (understood or read), printing counts by readable name only.

The pins' audit (`protocol::audit`, ignored unless run with `--ignored`; it reads `artifacts/protocol/<build>.json`, or
the folder `D3B_SCHEMAS` names) starts from each pinned pair and follows the messages their fields hold: every field
our `.proto` declares must be in the build's message with its number, kind (an `int32` may read an enum), list,
presence and oneof, each of our messages paired with one of the build's, the digest the pins'. `build.rs` describes
our messages for it in `OUT_DIR/protocol-shapes.txt`. On 2026-11-12, 3.7.13.17's 15 pins hold. The protocol's replay
(`protocol::replay`, `D3B_CAPTURES`) read the owner's 54 game connections of 3.7.13.17: of 3,048,392 frames, 282,806
understood, 256,933 read, 2,508,653 unknown (messages not pinned) and none not trusted; each of the 15 messages was
decoded, as many times as the study counted them.
