---
paths:
  - ".github/**"
---

# CI and releases

- **CI** (`ci.yml`) runs on Ubuntu only: the same checks as `bun run check`, the bindings' freshness, the PR's size and
  its Conventional Commits title. The three OSes are built by the release workflow.
- **Releases**: release-please keeps a release PR from the Conventional Commits on `next`; Claude merges it only on the
  owner's go. Its release triggers `release.yml`, which builds the six targets (Windows, macOS, Linux; x64 and ARM64),
  with the game data of the build named in `data-build.txt`, and attaches the installers and the updater's
  `latest.json`. Until `next` replaces `main`, releases are pre-releases.
- **No comments in workflows** (the owner's rule): a workflow reads by its step names; the reasons go in the ADRs and
  `docs/`. The one exception is the version after a pinned SHA (`@<sha> # v7.0.1`), which Dependabot reads and updates.
- **Actions are pinned** to a full commit SHA; Dependabot raises them weekly.
- **Secrets** come only from `secrets.*`, passed through `env:`, never interpolated into a `run:` line; untrusted
  inputs (PR titles, branch names) also go through `env:`. Default `permissions: contents: read`, widened per job.
