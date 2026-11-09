---
name: close-session
description: Close a session - check the docs follow the code, then report to the owner and give the prompt of the next session.
---

The repository is the only memory between sessions: what is not in it is lost.

1. Read `git log main..HEAD --oneline` and `git status`: what this session changed.
2. Check the docs follow it: `docs/status.md` (its "As of" line, what exists, what is not measured, what comes next, the
   known debt), the step's row in `docs/roadmap.md`, one line in `docs/decisions-index.md` per decision,
   `docs/architecture.md` where a module changed. Fix what lags.
3. Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` and read the results.
4. Report to the owner, in French, in this order:
   - what was done;
   - the decisions taken, by number;
   - the remaining risks, and what was not measured or checked;
   - the project's state: the checks' results, the branch, whether it is pushed;
   - the recommended next task, one;
   - a prompt for the next session in a fenced block, ready to copy: the task, the files to read first, the constraints.
5. Unfinished work goes in the report with the prompt, not in a file in the repository.

Done when every claim of the report comes from a command run in this step or a file read in it.
