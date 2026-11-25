# Spanish beside French and English (D554)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the studio's
two languages of [D14](d001-d018-foundation.md#d14-avalonia-and-shadui-in-french-and-english), the modules' texts of
[D96](d091-d102-step-5.md#d96-a-plugins-page-in-the-apps-rail) and the pack's texts of
[D25](d024-d033-step-2.md#d25-the-pack-format).

## D554: Spanish is the app's third language, its texts translated and the game's own read from the client's Spanish, pack format 15

*2026-11-25, outside the roadmap's steps, the owner's « faudrait faire un support espagnol en + de fr / en »; the game's
texts in Spanish too, the owner's choice among the two the lead put. Refines D14, D25, D96.*

- **Measured.** The client ships five text files, `de.bin`, `en.bin`, `es.bin`, `fr.bin` and `pt.bin`
  ([client.md](../client.md#texts)); on 3.7.14.18 the Spanish holds each of the 177,264 numbered texts the pack keeps,
  as French and English do, so that no table loses a name and the NPCs keep their 64,170 replies and 47,540 questions.
- **The pack, format 15.** `TextLanguages.All` is French, English and Spanish, so that the extractor keeps `texts/es` beside
  `texts/fr` and `texts/en`: the pack of 3.7.14.18 is 115,264,981 bytes, about 3.35 MB more, 3%, extracted in 59 s.
  Format 14 (D520), never published, is left: a pack of 14 written before has no Spanish, which the app would fail to read
  once in Spanish. `TextLanguages.Of` gives the pack's language of the app's, any
  other than the three English.
- **The texts.** Every `.resx` of the studio and the modules has its `.es.resx`, 1,853 texts, translated by the lead from
  the English with the owner's French beside it, to one glossary of the game's Spanish words (PA, PM, merkasako, jalató,
  mazmorra, desafío). A test fails on a key one language lacks, the studio's and each module's.
- **Chosen.** The settings' segment says Français, English, Español; the language starts as the one the app was left in,
  else the system's when it is one of the three, else English. Spanish counts as English does, 0 plural.
- **Not measured.** No Spanish speaker has read the texts, and the lead's glossary was not checked against the Spanish
  client's own words: a player's corrections are expected. The settings were seen in Spanish in the headless
  drawings of their test, each section fitting; the running app and its other pages were not.
- **Rejected.**
  - The studio in Spanish with the game's names in English, the pack left as it was: quicker, but a Spanish window would
    mix the two languages on every card and journal line.
  - German and Portuguese at the same time, which the client also ships: not asked, and every text added is one more to
    keep in each language.
  - Putting the Spanish into format 14 since it was never published: a pack of 14 already extracted on a machine would be
    read as holding Spanish, and the format is cheap to change.
