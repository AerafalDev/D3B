# Blueprints v2.3, the flow's kinds

Status: open

**Blueprints v2's (v2.3), the flow's kinds** ([bot.md](../bot.md#the-flows-kinds-v23)), on Windows x64, in a copy of
mine (nothing acts in the game before step 4): (1) Space, type "boucle": Répéter, Tant que, Pour chaque and Sortir
de la boucle; (2) a Sortir de la boucle wired after On start: the Library counts one problem, "Sortir de la boucle
n'est dans le corps d'aucune boucle."; moved into a Répéter's body: the problem goes; (3) the Summary of On start →
Répéter 3 → body: Attendre que Vie % ≥ 90 (au plus 5 min), Trop long → Prévenir; then Selon Métier with Bûcheron
and Mineur; reads "Répéter 3 fois", "À chaque tour :", "Attendre que … (au plus 5 min)", "Trop long :", "Bûcheron
→ … · Mineur → … · Sinon → …"; (4) an Essayer jusqu'à 3 fois around a call of a function of mine: "Essai :" and
"Si tous les essais échouent :"; an Au plus 1 fois reads "Une seule fois", a wire into its Réarmer "Réarmer N.";
Alterner reads "D'abord → … · Ensuite → …"; (5) `cargo test --manifest-path src-tauri/Cargo.toml -- --ignored
the_owners_game_captures_run_v2s_flow` with `D3B_CAPTURES` set: it passes, no run blocked but by a death;
