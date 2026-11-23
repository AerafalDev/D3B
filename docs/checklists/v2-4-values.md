# Blueprints v2.4, the values

Status: open

**Blueprints v2's (v2.4), the values** ([bot.md](../bot.md#the-values-v24)), on Windows x64, in a copy of mine
(nothing acts in the game before step 4): (1) Space: the group "Conditions et calculs" lists the comparisons, ET, OU,
NON, the Si … : … sinon …, the arithmetic, the draws, the lists, Texte and Joindre; (2) a `… < …` placed: drawn
compact, "<" in its middle, no head, its inputs unlabelled; 80 typed on its second input shows as a chip beside its pin,
gone once a wire feeds it; its whole sentence on hover; (3) Pods % wired into that `<`, the `<` into a Tant que: the
graph's and the Summary's sentence read "Tant que Pods % < 80"; a variable Récoltes into a `+` with 5, the `+` into a
`×` with 2: the `×` reads "(Récoltes + 5) × 2"; an ET of three inputs reads "… ET … ET …"; (4) a `÷` with 0 typed on
its divisor: the Library counts one problem, "L'entrée … divise par 0 : le personnage serait bloqué."; a 0 that comes
by a wire is no problem; (5) an ET's "Ajouter une entrée" adds a third input, the right-click on it "Retirer" removes
it and its wire, greyed at two; a Liste likewise, from one; (6) a Texte, "Récolté {n} en {temps}" typed in Details'
"Modèle du texte" and left: the pins `n` and `temps` appear; renaming `{n}` drops its wire; a Prévenir fed by it reads
the template filled; (7) a Si … : … sinon … typed by the wire into its first value, the other value's pin takes the
same type; (8) D3B's blueprints show any value they hold compact and read-only, a Texte's template shown, not editable;
(9) the app in English: the same nodes read "AND", "While Pods % < 80" and "(Récoltes + 5) × 2";
(10) `cargo test --manifest-path src-tauri/Cargo.toml -- --ignored the_owners_game_captures_run_v2s_values` with
`D3B_CAPTURES` set: it passes, no run blocked but by a death.
