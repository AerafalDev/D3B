import type { Edge, NodeChange } from "@xyflow/react";

import type { GraphView_Serialize } from "@/bindings.ts";
import type { BoxNode } from "./blueprint-node.tsx";
import { graphSentences } from "./node-sentence.ts";
import type { PillNode } from "./variable-pill.tsx";

// Where a node the file does not place goes: a column on the left, one under the other, far enough apart for a box of
// several rows.
export const UNPLACED_X = -360;
export const UNPLACED_STEP = 200;

/**
 * A graph's nodes and wires as React Flow draws them: each node where the file puts it, else in the column; a get as
 * a pill; each wire classed by what its source pin carries, for its colour.
 */
export function graphElements(
  graph: GraphView_Serialize,
  names: ReadonlyMap<string, string>,
  faulty: ReadonlySet<string> = new Set(),
  chosen: string | null = null,
): { nodes: (BoxNode | PillNode)[]; edges: Edge[] } {
  let unplaced = 0;
  const byId = new Map(graph.nodes.map((node) => [node.id, node]));
  const sentences = graphSentences(graph, names);
  const nodes = graph.nodes.map((node): BoxNode | PillNode => {
    const [x, y] = node.position ?? [UNPLACED_X, UNPLACED_STEP * unplaced++];
    const position = { x, y };
    if (node.does === "get") {
      const valueType = node.pins[0]?.carries ?? "text";
      const data = { name: names.get(node.variable) ?? node.variable, valueType, faulty: faulty.has(node.id) };
      return { id: node.id, type: "pill", position, data, selected: node.id === chosen };
    }
    const data = { node, sentence: sentences.get(node.id) ?? [], faulty: faulty.has(node.id) };
    return { id: node.id, type: "box", position, data, selected: node.id === chosen };
  });
  const edges: Edge[] = graph.wires.map(({ from, to }) => {
    const carries = byId.get(from[0])?.pins.find((pin) => pin.id === from[1] && pin.side === "out")?.carries ?? "exec";
    return {
      id: `${from.join(".")}>${to.join(".")}`,
      source: from[0],
      sourceHandle: from[1],
      target: to[0],
      targetHandle: to[1],
      className: `blueprint-type-${carries}`,
    };
  });
  return { nodes, edges };
}

/** The node React Flow's changes choose, by a click or by Enter on a focused node, if any. */
export function chosenNode(changes: readonly NodeChange[]): string | null {
  const chosen = changes.find((change) => change.type === "select" && change.selected);
  return chosen?.type === "select" ? chosen.id : null;
}
