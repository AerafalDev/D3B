# 0005. Claude delivers through squash-merged PRs on protected branches

- Status: accepted
- Date: 2026-11-11
- Decided by: the owner

## Context

In the C# version every push, PR and merge waited for the owner's explicit go, which slowed everything; the owner now
wants Claude to commit, open and merge its own PRs, with the branches protected.

## Decision

- `main` and `next` are protected by GitHub rulesets: changes through PRs only, the CI check required, no force-push,
  no deletion. The repository allows squash merges only and deletes merged branches.
- Claude branches from `origin/next`, commits in Conventional Commits, opens the PR, has a fresh `reviewer` agent read
  it, waits for CI and squash-merges (`pr` skill). PRs carry one behaviour within 600 handwritten lines.
- No AI attribution in commits or PRs.
- Releases, data releases and tags wait for the owner's go.
- Every choice that is not forced is the owner's, asked with AskUserQuestion; costly ones become ADRs; a feature is
  done once the owner measured it in game.

## Rejected

- The owner's go on every push (the C# version): safe but slow.
- Claude merging without protected branches: one mistake away from a broken `next`.

## Consequences

The owner reviews after the merge, through the PRs and `docs/now.md`. Permission denies keep Claude from pushing to
`main` or `next` directly, force-pushing or merging any other way.
