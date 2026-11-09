---
name: ship
description: Get a finished task ready to deliver - branch, checks, docs, review, commit - then stop and ask the owner before anything leaves the machine.
argument-hint: "The task"
---

Nothing is pushed, published or merged without the owner's explicit go for that action; a hook asks again at the push and
at the pull request. One branch and one pull request per session.

1. Work on a branch cut from an up-to-date `main`, named as the `git` skill says.
2. Run the checks that CI runs, and read their output: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`,
   and `dotnet build scripts/<tool>.cs` or `dotnet build .claude/hooks/<hook>.cs` for each one the branch changed.
3. Bring the docs level with the code: `docs/status.md`, the step's row in `docs/roadmap.md`, a line in
   `docs/decisions-index.md` for each decision, and the sections of `docs/architecture.md` the change touches.
4. Run `/review`; fix its blocking findings; run step 2 again.
5. Commit as the `git` skill says.
6. Stop. Tell the owner, in French: the branch, the commits, the checks' results, what was not checked. Ask for the go to
   push.
7. After a go to push: push the branch and open the pull request, as the `git` skill says. After a separate go to merge:
   merge, and say so.

Done when the owner holds a green branch and the question; a red check is fixed on the branch, never bypassed.
