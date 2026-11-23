# Step 3, the Library

Status: open

**step 3, the Library**, on Windows x64 from `bun run app` on `next` or the next release:

1. the activity bar has "Bibliothèque" under "Personnages"; it lists "Donnés par D3B": Récolte automatique, Récolte
   d'un métier, Chasse (in English after switching the language), and "Les miens", empty at first;
2. click Récolte automatique: its tab says "Donné par D3B · v1 · verrouillé", its description, and its settings:
   Zone à choisir, Banque à 90 %, Banque la plus proche, Garder rien;
3. Dupliquer (the tab's button or a right-click): "Récolte automatique (copie)" under "Les miens", its tab says
   "À moi · basé sur Récolte automatique v1", `Documents/D3B/blueprints/recolte-automatique-copie.json` exists,
   and it is still listed after a restart;
4. a blueprint's tab shows "Mon blueprint" on the left (Graphe d'événements, the variables with an eye on Zone, Banque
   à, Banque, Garder) and the graph: Au lancement → Aller dans [Zone] (étape 4) → Récolter [Tout] dans [Zone]
   jusqu'à [Banque à] de pods (étape 5) → Aller à [Banque] → Déposer à la banque, garder [Garder], then back; Vie
   sous [40 %] → Attendre [90 %] de vie; no node over another; pan and zoom; nothing can be moved or wired;
5. click Vie sous 40 %: Details on the right says its sentence, its inputs (Seuil 40 %, Réarmement 60 %), priority 1,
   and "Interrompre, puis reprendre" marked among the four modes, each drawn; click Récolter: "Le bot le fera à
   partir de l'étape 5" and what an unlinked Échec does; click Banque à in Mon blueprint: Pourcentage, 90 %, de 10
   à 100, set per character, 1 nœud; click the empty graph: "Choisis un nœud ou une variable.";
5b. with the blueprint's tab active, "Graphe | Résumé" sits at the right of the tabs; Résumé shows "Au lancement"
   with steps 1 to 4 (Aller dans [Zone] étape 4 … Déposer à la banque, garder [Garder]) and "Revenir à 1.", then
   "Vie sous [40 %] · priorité 1 · Interrompre, puis reprendre" with Attendre [90 %] de vie, then the three
   defaults (Un combat commence, Le personnage est mort, Une action échoue · par défaut); click step 2: Details
   shows Récolter; Graphe brings the graph back; a character's tab shows no toggle;
6. put a broken file in `Documents/D3B/blueprints` (a stray letter), then show the Library again (Personnages,
   then Bibliothèque): "Ignorés 1" appears folded at the bottom; open it: the file, and why in its hover.
