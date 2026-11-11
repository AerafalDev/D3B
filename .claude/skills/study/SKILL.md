---
name: study
description: Study how one behaviour of Dofus 3 works (a protocol exchange, a game rule, a data table) before designing or coding it, with each finding's provenance. Use before anything that reproduces the game.
argument-hint: "The behaviour, such as 'how the server answers a harvest request'"
---

1. Look for an existing study in `artifacts/studies/`, then in the C# version's measured docs:
   `git show main:docs/client.md`, `main:docs/client-game.md` (grep their headings first; they are large).
2. Give the `protocol-researcher` agent the behaviour and the questions the feature needs answered. It writes
   `artifacts/studies/<subject>.md`, which Git ignores since it quotes the client.
3. Read its summary. Each finding is `verified` (a capture or the client's data) or `inferred` (docs or another
   reference only). An `inferred` finding that decides the design becomes a question for the owner, or a capture the
   owner can take: say exactly what to do in game and what to record.
4. Cite the study and the provenance in the design and in the doc comment of the code that implements it, without
   quoting the client beyond names and values.
