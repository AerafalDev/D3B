# 0009. Flags are written by the names the client's enums give them, read with Cpp2IL

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

Many records hold a bit field, `m_flags`: map positions, spells, spell levels, effects, monsters, items. The client's
code names each bit in an enum, such as `MapInformationFlags` or `MonsterFlags` (`main:docs/client.md`, "Fights'
tables"), which the data tables do not carry: only its IL2CPP metadata does.

## Decision

- **The extractor reads the enums from the client's code** with Cpp2IL (`Samboy063.Cpp2IL.Core`, MIT, the
  pre-release the C# version used and step 2's protocol schema needs too), so that each build's names come from that
  build.
- **A record's flags are the list of the names of the bits it sets**, in camelCase, such as
  `"flags": ["allowTeleportTo", "allowTeleportFrom"]`. A bit the enum does not name fails the extraction.

## Rejected

- A reader of our own for `global-metadata.dat` (version 39): more code to keep up with each Unity, for what Cpp2IL
  already reads.
- The raw number, decoded in Rust when a step needs it: names never checked against the build.
- An object of every bit as a boolean, or the number beside the names: larger, or the same fact twice.

## Consequences

The extraction opens the client's `GameAssembly.dll` and metadata, a few seconds more. The app reads flags as Rust
enums: a name it does not know refuses the folder, so a build that adds a bit needs a new release of both.
