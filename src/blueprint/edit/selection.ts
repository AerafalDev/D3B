/** What a node or a wire drawn says of its choosing. */
interface Chosen {
  id: string;
  selected?: boolean;
}

/**
 * The graph saved drawn again, keeping the nodes and wires the player chose in it (step 3's PR 13: several chosen
 * are deleted together), those the graph no longer holds gone. A node chosen elsewhere, which the player did not
 * choose in the graph (Details, the Summary), takes the selection's place.
 */
export function keptSelection<N extends Chosen, E extends Chosen>(
  drawn: { nodes: readonly N[]; edges: readonly E[] },
  now: { nodes: readonly Chosen[]; edges: readonly Chosen[] },
  chosen: string | null,
): { nodes: N[]; edges: E[] } {
  const ids = (items: readonly Chosen[]) => new Set(items.filter((item) => item.selected === true).map((it) => it.id));
  const nodes = ids(now.nodes);
  if (chosen !== null && !nodes.has(chosen)) {
    return { nodes: [...drawn.nodes], edges: [...drawn.edges] };
  }
  const edges = ids(now.edges);
  const keep = <T extends Chosen>(items: readonly T[], kept: Set<string>) =>
    items.map((item) => (kept.has(item.id) && item.selected !== true ? { ...item, selected: true } : item));
  return { nodes: keep(drawn.nodes, nodes), edges: keep(drawn.edges, edges) };
}
