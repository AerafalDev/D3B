# The status bar all in icons on the right, the docks' content held while they slide (D546)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the slides of
[D545](d545-slides-and-corners.md).

## D546: The relay and the version join the status bar's icons on the right, and a part's content keeps its size while it slides

*2026-11-25, the owner's asks in the running app: « faudrait faire la même chose pour relais prêt et le mettre à droite, et pareil
pour la v de D3B », and of the slides: « le contenu du dock bouge et ça fait un peu n'importe quoi ». Refines D541, D544 and D545.*

- **The status bar's items are all on the right**, icons and figures whose words are their tooltips (D544): the relay's dot, its
  colour where it stands (red once stopped), with the characters it carries beside it once ready, « Relais prêt · N en jeu » and
  its advice in the tooltip (`RelayFigure`, `RelayHover`); then the client, the bots, the problems, the saving, the update; the
  version's blue dot last, « D3B 0.9.10 · À propos » in its tooltip (`VersionTip`), About on a click.
- **A part's content keeps its size while it slides** (`MainWindow.Hold`, `DockEdge`): fixed against the edge it slides from and
  clipped by its dock, as VS Code's, it is uncovered or covered instead of laid out again at each step, which moved the views that
  adapt to their width. Once the slide ends, it fills its dock again.
- **Rejected.**
  - Keeping « Relais arrêté » in words: the red dot and the problems' count, which the failure raises (D543), already say it,
    and the tooltip and a click give why.
