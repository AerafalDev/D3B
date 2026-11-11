---
name: adr
description: Record a decision the owner made that is costly to reverse, as a short MADR-minimal ADR and its line in the index. Use right after such a decision; small choices go in the PR instead.
argument-hint: "The decision"
---

An ADR is written only for a choice costly to reverse, contested, or likely to be undone by a later session without
it. It records a decision the owner made, never one Claude made alone.

1. Take the next number in `docs/adr/` (four digits).
2. Write `docs/adr/NNNN-<the-decision-in-kebab-case>.md`, 15 to 40 lines, in English:

   ```markdown
   # NNNN. <The decision, as a sentence>

   - Status: accepted
   - Date: <YYYY-MM-DD>
   - Decided by: the owner

   ## Context
   <What forces the choice, in a few lines.>

   ## Decision
   <The choice, with the reason for each part that is not obvious.>

   ## Rejected
   - <Alternative>: <why, one line>.

   ## Consequences
   <What it makes easy, what it makes hard, what must now hold (and how review checks it).>
   ```

3. Add its line to the table of `docs/adr/README.md`: number, title, status.
4. An ADR is never rewritten. A later decision gets a new ADR that says `Supersedes NNNN`; the earlier one's status
   becomes `superseded by MMMM`, which is the only edit it ever gets.
