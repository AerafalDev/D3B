---
name: dependency
description: Propose a new dependency (a crate or an npm package) to the owner, or raise a pinned toolchain, with its reason, size, licence and alternatives. Use before adding anything to Cargo.toml or package.json, and for Dependabot's PRs.
argument-hint: "The package, or the toolchain to raise"
---

## A new dependency

1. **Do we need it?** Say what the need is, and what our own code would take (lines, risk).
2. **The candidate**, measured, not recalled: version, licence (MIT, Apache-2.0, BSD, ISC, Zlib, MPL-2.0 are fine;
   anything else is a question), size (`cargo tree -p <crate> -e normal` after a trial `cargo add` in a scratch
   copy, or the published unpacked size for npm), its own dependencies, maintenance (last release, open issues),
   and for Rust whether it builds on the six targets.
3. **Alternatives**: one or two, with the same facts.
4. **Ask** with AskUserQuestion, the recommendation first and why. On a yes: `cargo add <crate>@<version>` from
   `src-tauri/`, or `bun add <name>@<version>` (`-d` for a tool); `bun install --frozen-lockfile` and `cargo check`
   pass; the reason goes in the PR.

## Raising a toolchain or a pinned package

Rust (`rust-toolchain.toml`), Bun (`packageManager`), TypeScript, Tauri, `specta`/`tauri-specta` (pinned rc) and
Biome are raised on purpose: read the release notes between the two versions, list what touches us, raise every
place that pins it, run `bun run check`, and say what was read in the PR. Dependabot's PRs go the same way, never
merged unread.
