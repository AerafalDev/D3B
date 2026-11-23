import type { Edge } from "@xyflow/react";

import type { GraphEdit_Deserialize } from "@/bindings.ts";
import type { GraphNode } from "../node-sentence.ts";
import { optionPin } from "./node-options.ts";

/** What a right-click in the graph landed on: a node or a wire, by its id, or a pin "Remove" removes. */
export type MenuTarget =
  | { kind: "node"; id: string }
  | { kind: "wire"; id: string }
  | { kind: "pin"; id: string; pin: string };

/**
 * What a right-click on a node of the graph landed on: a numbered pin or a case under the pointer, its dot or its row,
 * has its own menu, "Remove" (the owner, 2026-11-17), which never deletes the node, even at its kind's least; anything
 * else of the node, the node. A box's and a compact value's alike.
 */
export function rightClicked(at: EventTarget | null, node: { id: string; type?: string; data: unknown }): MenuTarget {
  const element = at instanceof Element ? at : null;
  const row = element?.closest(".blueprint-row");
  const handle = element?.closest(".react-flow__handle") ?? row?.querySelector(".react-flow__handle");
  const pin = handle?.getAttribute("data-handleid") ?? null;
  const drawn = (node.type === "box" || node.type === "value") && (node.data as { node?: GraphNode }).node;
  return pin !== null && drawn && optionPin(drawn, pin)
    ? { kind: "pin", id: node.id, pin }
    : { kind: "node", id: node.id };
}

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
 * another node, that node; from a wire, that wire; from a pin, nothing.
 */
export function menuDeletion(
  target: MenuTarget,
  nodes: readonly { id: string; selected?: boolean }[],
  edges: readonly Edge[],
): GraphEdit_Deserialize | null {
  // A pin's menu removes the pin, never its node.
  if (target.kind === "pin") {
    return null;
  }
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
