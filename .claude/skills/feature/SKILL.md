---
name: feature
description: Design and deliver one D3B feature end to end, every choice decided by the owner, ending on a measure in game. Use when the owner names a feature or a roadmap step.
argument-hint: "The feature, such as 'the character's live view'"
---

1. **Know the game.** For each behaviour the feature needs, find its study in `artifacts/studies/`; when one is
   missing or leaves a question open, run `study` first. Nothing is designed on a guess.
2. **Design.** Give the `architect` agent the feature, the studies and the code it touches. It returns the scope, the
   data flow, the placement, the checks with the in-game checklist, the questions and the PR stack.
3. **Decide with the owner.** Put the questions with AskUserQuestion, at most four at a time, the recommendation
   first, saying why and whether something better exists. For an interface, show a mockup first (a React page under
   `bun run dev`, or an artifact) and ask on it. When an answer changes the scope, ask the architect again rather
   than patching. No code before every blocking question is answered.
4. **Write it down.** The decided design goes into the domain's page `docs/<domain>.md` (three lines of summary, a
   Mermaid diagram, then the details; about two pages at most). `docs/now.md` names the stack. A choice costly to
   reverse also gets an `adr`.
5. **Deliver** the stack in order, one PR at a time through `tdd` and `pr`.
6. **Measure in game.** At the end of the stack, give the owner the checklist (in `docs/now.md` under "Not measured")
   and wait for the result. A failed step becomes a bug (`diagnose`) in the same feature. The feature is done when
   every step passes; `docs/now.md` then moves it to "Done" and names the next step.
