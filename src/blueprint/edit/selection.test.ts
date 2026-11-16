import { describe, expect, it } from "vitest";

import { keptSelection } from "./selection.ts";

const drawn = (chosen: string | null) =>
  ["n1", "n2", "n3"].map((id) => ({ id, selected: id === chosen, position: { x: 0, y: 0 } }));
const wires = (...ids: string[]): { id: string; selected?: boolean }[] => ids.map((id) => ({ id }));
const local = (...selected: string[]) => ["n1", "n2", "n3"].map((id) => ({ id, selected: selected.includes(id) }));

describe("keptSelection", () => {
  it("keeps the nodes and wires the player chose when the graph saved is drawn again", () => {
    const kept = keptSelection(
      { nodes: drawn("n2"), edges: wires("a>b", "b>c") },
      { nodes: local("n1", "n2"), edges: [{ id: "a>b", selected: true }] },
      "n2",
    );
    expect(kept.nodes.map((node) => node.selected)).toEqual([true, true, false]);
    expect(kept.edges.map((edge) => edge.selected)).toEqual([true, undefined]);
  });

  it("drops what the graph saved no longer holds", () => {
    const kept = keptSelection({ nodes: drawn(null).slice(1), edges: [] }, { nodes: local("n1"), edges: [] }, null);
    expect(kept.nodes.map((node) => [node.id, node.selected])).toEqual([
      ["n2", false],
      ["n3", false],
    ]);
  });

  it("takes a node chosen elsewhere (Details, the Summary) in place of the selection", () => {
    const kept = keptSelection(
      { nodes: drawn("n3"), edges: wires("a>b") },
      { nodes: local("n1", "n2"), edges: [{ id: "a>b", selected: true }] },
      "n3",
    );
    expect(kept.nodes.map((node) => node.selected)).toEqual([false, false, true]);
    expect(kept.edges.map((edge) => edge.selected)).toEqual([undefined]);
  });
});
