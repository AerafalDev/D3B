---
name: verify
description: Verify before claiming that anything is done, fixed or passing. Use before every "done", every PR and every report to the owner.
---

No claim without evidence from this session.

1. Name what would prove the claim: a test, `bun run check`, a screenshot of the running app (`run`), a command's
   output.
2. Run it now, in full, and read the output: the totals, the ignored tests (those needing real data or captures) and
   the warnings, not only the exit code.
3. Report what the evidence shows, with its command: "5 passed, 0 failed, 1 ignored (`cargo test`)". A check not run
   is said as not run, a partial result as partial, and what only the owner can run in game as such.
4. When the evidence contradicts the claim, the claim is wrong: fix, then verify again.
