---
paths:
  - ".github/**"
---

# CI and releases

- **CI** (`ci.yml`) runs on Ubuntu the same checks as `bun run check`, the extractor's included, in the
  `Check` job; then the freshness of the bindings and of `docs/protocol-coverage.md`, the PR's size (skipped when the
  owner granted an exception: the `size-exception` label), its Conventional Commits title, and the cap on open checklists (a `feat` PR
  fails while more than five are open, unless labelled `step-finish`; `scripts/check-checklists.ts`). A `Windows` job
  runs Vitest, `cargo test` and the same freshness on `windows-latest`, where the owner plays (the owner,
  2026-11-23); its Rust cache is saved from `next` only, so that PRs' Windows targets do not evict it. The rulesets
  require `Check` and `Pull request` (so the cap binds), not `Windows` until the owner adds it: the `pr` skill waits
  for every check all the same. The three OSes are built by the release workflow.
- **Releases**: release-please keeps a release PR from the Conventional Commits on `next`; Claude merges it only on the
  owner's go. Its release triggers `release.yml`, which builds the six targets (Windows, macOS, Linux; x64 and ARM64),
  with the game data of the build named in `.github/game-data.json`, and attaches the installers and the updater's
  `latest.json`. Until `next` replaces `main`, releases are pre-releases.
- **release-please runs as the owner's GitHub App** (variable `RELEASE_APP_CLIENT_ID`, secret `RELEASE_APP_PRIVATE_KEY`), never with
  `GITHUB_TOKEN`: GitHub runs no workflow on a PR that `GITHUB_TOKEN` opens, and the ruleset needs CI's checks.
- **Formatting**: Biome expands every JSON object and array, as release-please's `JSON.stringify` writes them; keep it
  so, or the release PR fails the format check.
- **No comments in workflows** (the owner's rule): a workflow reads by its step names; the reasons go in the ADRs and
  `docs/`. The one exception is the version after a pinned SHA (`@<sha> # v7.0.1`), which Dependabot reads and updates.
- **Tauri's system libraries** install in CI from cached `.deb` files (`actions/cache`, keyed by the runner's image,
  saved as soon as they install), and fall back to Ubuntu's mirror when the cache is missing or does not install; the
  step stops after 5 min. On 2026-11-11 the runners lost Ubuntu's mirrors for hours and every job hung for its 30 min.
  A changed package list is cached with the next image. The release workflow still installs from the mirror.
- **Actions are pinned** to a full commit SHA; Dependabot raises them weekly.
- **Secrets** come only from `secrets.*`, passed through `env:`, never interpolated into a `run:` line; untrusted
  inputs (PR titles, branch names) also go through `env:`. Default `permissions: contents: read`, widened per job.
