---
paths:
  - ".github/**"
---

# CI and releases

- **CI** (`ci.yml`) runs on Ubuntu only: the same checks as `bun run check`, the bindings' freshness, the PR's size (skipped
  when the owner granted an exception: the `size-exception` label) and its Conventional Commits title. The three OSes are built by the release workflow.
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
- **Actions are pinned** to a full commit SHA; Dependabot raises them weekly.
- **Secrets** come only from `secrets.*`, passed through `env:`, never interpolated into a `run:` line; untrusted
  inputs (PR titles, branch names) also go through `env:`. Default `permissions: contents: read`, widened per job.
