import type { Edge } from "@xyflow/react";

import type { GraphEdit_Deserialize } from "@/bindings.ts";

/** What a right-click in the graph landed on: a node or a wire, by its id in the graph drawn. */
export type MenuTarget = { kind: "node"; id: string } | { kind: "wire"; id: string };

/**
 * The edit that deletes `nodes`, each with its wires on the Rust side, and `edges` (step 3's PR 13): one change, which
 * one undo brings back whole; `null` when nothing is chosen.
 */
export function deletion(nodes: readonly string[], edges: readonly Edge[]): GraphEdit_Deserialize | null {
  if (nodes.length === 0 && edges.length === 0) {
    return null;
  }
  const wires = edges.map((edge) => ({
    from: [edge.source, edge.sourceHandle ?? ""] as [string, string],
    to: [edge.target, edge.targetHandle ?? ""] as [string, string],
  }));
  return { edit: "delete", nodes: [...nodes], wires };
}

/**
 * What the context menu's Delete deletes: from a node in the selection, the whole selection, as Unreal does; from
 * another node, that node; from a wire, that wire.
 */
export function menuDeletion(
  target: MenuTarget,
  nodes: readonly { id: string; selected?: boolean }[],
  edges: readonly Edge[],
): GraphEdit_Deserialize | null {
  if (target.kind === "wire") {
    const edge = edges.find((one) => one.id === target.id);
    return edge === undefined ? null : deletion([], [edge]);
  }
  const node = nodes.find((one) => one.id === target.id);
  if (node === undefined) {
    return null;
  }
  if (node.selected !== true) {
    return deletion([node.id], []);
  }
  const chosen = nodes.filter((one) => one.selected === true).map((one) => one.id);
  return deletion(
    chosen,
    edges.filter((edge) => edge.selected === true),
  );
}
