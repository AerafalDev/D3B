---
name: decision
description: Record a decision as a new entry at the end of the last file of docs/decisions/ with its line in docs/decisions-index.md. Use once the owner has settled an important or hard-to-reverse choice.
argument-hint: "The subject"
---

Only a choice that is hard to reverse, or that the owner settled, is recorded; a local, reversible one is just made.

1. Take the next number: the last line of `docs/decisions-index.md` names the last `D<n>`.
2. Add the entry at the end of the last decisions file, the one `docs/decisions/README.md` lists last (a new file, named
   `d<n>-<short-name>.md` and listed there, once that one is about 100 KB), in English:
   - `## D<n>: <Title>`, then an italic line: the date, the step or session, who asked or decided, and `Builds`,
     `Refines` or `Supersedes` followed by the decisions it leans on.
   - Bullets of one to three sentences: what was found (measured, with where), what was made, and **Rejected** with each
     alternative and why.
3. Add its line to `docs/decisions-index.md`, in the group of its step, or in a new `## <Group> (D<n>)` heading:
   `- [D<n>](decisions/<file>.md#<anchor>) <Title> (<date>)`. The anchor is the heading in lowercase, punctuation removed,
   spaces as dashes.
4. Never edit an earlier entry. A decision that replaces another says `Supersedes D<m>` in its own italic line.

Done when `git diff docs/` shows only added lines in the decisions file, and the index line's anchor spells the new heading.
