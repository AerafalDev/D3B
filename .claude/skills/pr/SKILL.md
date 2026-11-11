---
name: pr
description: Commit, check, review, open and squash-merge a pull request for the current work. Use when a branch's work is complete.
---

Claude merges its own PRs once everything is green (CLAUDE.md, Delivery). Each step passes before the next.

1. **Branch**: never commit or push on `next` or `main` (`git push origin HEAD` there slips past the permission
   denies; GitHub's ruleset refuses it anyway). `git switch -c <type>/<subject>` from an up-to-date `origin/next`
   (`git fetch` first), such as `feat/relay-framing`.
2. **Commit** only what belongs to the change (`git status`, then `git add <paths>`): nothing under `data/` or
   `artifacts/`, nothing derived from the client. Messages are Conventional Commits:
   `<type>(<scope>): <what changes, imperative>`, at most 100 characters; types `feat`, `fix`, `perf`, `refactor`,
   `test`, `docs`, `build`, `ci`, `chore`; scope the domain (`relay`, `data`, `ui`, `claude`, `deps`); a body for why.
   No AI attribution.
3. **Size**: `bun run pr:size`. Over 600 lines, split into a stack before going on, unless the owner granted an
   exception: then the PR carries the `size-exception` label (`gh pr edit <n> --add-label size-exception`).
4. **Checks**: `bun run check` in full, then `git diff --exit-code src/bindings.ts` (stale bindings fail CI). For an
   interface change, a screenshot through `run`.
5. **Review, one round**: `git diff origin/next...HEAD > artifacts/review.diff`, then one fresh `reviewer` agent with
   the diff and the design or task. Fix every blocking finding; list the others in the PR. No second round for
   details.
6. **Push and open**: `git push -u origin HEAD`, then `gh pr create --base next --title "<conventional header>"
   --body-file <file>` (the title becomes the squash commit; CI checks it). The body follows
   `.github/pull_request_template.md`.
7. **CI**: `gh pr checks <n> --watch`. A red check is fixed on the branch, never bypassed.
8. **Merge**: `gh pr merge <n> --squash --delete-branch`, then `git switch next && git pull --ff-only`.
9. **`docs/now.md`** says what is done and what comes next, in the PR that ends a step.
