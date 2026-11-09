---
name: review
description: Review the branch's diff once, with the fresh reviewer agent, before it is delivered.
argument-hint: "The roadmap step or task the branch implements"
---

The author does not review its own diff: a fresh agent does, knowing only the diff, the task and the repository.

1. Save the diff of the whole branch against `main`, committed or not, the branch's slashes made dashes: first
   `git add -N <new files>`, since a diff skips untracked files, then
   `mkdir -p artifacts/review && git diff main > artifacts/review/chore-claude-environment.diff` for
   `chore/claude-environment`. Read `git diff main --stat` to see that it holds what `git status` lists.
2. Start the `reviewer` agent with only three things: the diff file's path, the task in a sentence or the roadmap step it
   implements, and the numbers of the decisions it touches. Nothing of this session's reasoning: a reviewer that shares
   it confirms it.
3. Fix each finding it marks blocking, then run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes`.
   Keep the others for the pull request's body.
4. When a finding doubts a test, break the line the test guards and watch it fail; if it still passes, fix the test.

Done when no blocking finding is left and the three commands are green.
