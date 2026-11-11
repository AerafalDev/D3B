---
name: tdd
description: "Write a behaviour test-first in Rust or TypeScript: a failing test, the least code to pass it, its failure case, then the cleanup. Use for every behaviour and every bug fix."
---

The conventions are in `.claude/rules/tests.md`.

1. **Red**: one test for one behaviour, named after it. Run it alone
   (`cargo test --manifest-path src-tauri/Cargo.toml <name>`, `bunx vitest run <path> -t "<name>"`) and watch it fail
   for the expected reason, not a compile error or a wrong setup.
2. **Green**: the least code that makes it pass. Run it, then the folder's tests.
3. **Failure case**: the behaviour's edge (empty, maximal, invalid, boundary, the wire's truncated frame) gets its own
   test, red then green; an error in normal use is a typed result, so the test asserts its variant.
4. **Refactor** with the tests green: names, duplication, one subject per file, ten files per folder, `#[expect]`
   reasons.
5. A bug fix starts with the test that reproduces it (`diagnose`); it stays as the regression test.
