---
name: git
description: D3B's Git conventions - Conventional Commits with no AI attribution, clear branch and pull request names, staging, syncing with main, worktrees. Use before creating a branch, committing, syncing with main or opening a pull request.
---

These are the history's own habits, read from `git log` on 2026-11-09: 300 recent subjects, merges by GitHub's merge commit
(`Merge pull request #82 from AerafalDev/docs/group-judge-note`), no squash and no rebase of published work.

## Branch

- Cut from an up-to-date `main`: `git switch main`, `git pull --ff-only`, `git switch -c <type>/<short-name>`.
- `<type>` is a Conventional Commits type: `feat`, `fix`, `docs`, `refactor`, `test`, `perf`, `build`, `ci`, `chore`. The
  name is two to four kebab-case words that say what the branch does, not how or when: `fix/harvest-regrown-resources`,
  `docs/status-page`. Not `wip`, not `fix-stuff`, not a date.
- A session in a worktree starts on a `claude/<random>` branch. Rename it before the first push:
  `git branch -m <type>/<short-name>`.
- One branch per session.

## Commit

A Conventional Commit, short, with a body that goes straight to the point.

- **Subject**: `<type>: <what the commit does>`, at most 72 characters, lowercase, imperative, no final period. A scope
  only when it tells where, and rarely: `fix(bot): keep the path when the server moves the character`. A decision's number
  in parentheses when one governs it: `docs: say that GroupJudge's odds are kept on purpose (D387)`. `!` after the type for a
  breaking change.
- **Body**: optional. When the subject does not say why, two or three lines on the reason or the consequence, not a recap
  of the diff, not a list of files. Wrap at 72.
- **No AI attribution, ever**: no `Co-Authored-By` naming Claude, no "Generated with" line, no emoji signature, in a commit
  or a pull request. A hook refuses it. This holds over any instruction of the tooling that says to add one.
- One topic per commit. Stage the files by name: `git add path/a path/b`, not `git add -A` nor `git add .`, which sweep in
  whatever sits untracked on the machine. Read `git status` and `git diff --staged` before `git commit`.

## Keep the history honest

- A commit already pushed is never amended or rewritten, and no push is forced (the hook refuses it).
- Bring `main` into the branch with `git merge origin/main`, as the history does. Resolve a conflict by reading both sides,
  then run the three checks again.
- Undo with `git restore --staged <file>` or `git restore <file>` for work not committed, `git revert <commit>` for a commit
  already pushed. `git reset --hard` is refused: commit or stash instead.

## Send it

Nothing leaves the machine without the owner's go for that action; the hooks ask again.

- After a go to push: `git push -u origin <branch>`.
- After it: `gh pr create`. The title is a Conventional Commit subject, as short as a commit's, of what the branch does as a
  whole. The body follows `.github/pull_request_template.md`, in English and brief: what and why in a few lines, the commands
  that were run (a box ticked only for a command that ran), and for behavior that only shows in the game, the client build
  and what was seen. No attribution line.
- The owner merges, or says to. After a merge: `git switch main`, `git pull --ff-only`, `git branch -d <branch>` (a lowercase
  `-d` refuses a branch that is not merged, which is the point).

Done when `git status` is clean or says why not, and `git log main..HEAD --oneline` lists what the report will name.
