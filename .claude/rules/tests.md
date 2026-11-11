---
paths:
  - "**/*.test.ts"
  - "**/*.test.tsx"
  - "src-tauri/src/**/tests.rs"
  - "src-tauri/tests/**"
---

# Tests

- **Rust**: unit tests in a `#[cfg(test)] mod tests` at the end of the file, or in a sibling `tests.rs` when long;
  integration tests (relay and protocol over captures) in `src-tauri/tests/`. **TS**: Vitest with happy-dom, beside
  the code as `<subject>.test.ts(x)`; components with Testing Library.
- **One behaviour per test, with its failure case**; the name says the behaviour (`refuses_another_format`,
  `"falls back to English"`).
- **Deterministic**: a fixed clock and a seeded random source passed in; no real time, no sleep, no network, no
  writing outside a temporary folder the test removes.
- **Fixtures are ours**: hand-written JSON, synthetic frames. A test needing real client data or a real capture reads
  it from `D3B_DATA` or `D3B_CAPTURES` and is skipped (`#[ignore]` with the reason, `it.skipIf`) when they are unset;
  CI has neither.
- **Property tests** (`proptest`) for pathfinding, line of sight and codecs once they exist.
- A test that would still pass with the line it covers broken is not a test: watch it fail first (`tdd`).
