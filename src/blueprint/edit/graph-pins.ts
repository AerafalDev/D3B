import { createContext } from "react";

import type { GraphView_Serialize, PinSide } from "@/bindings.ts";
import type { LinkEnd } from "./link-check.ts";

/** A graph's pin by its node, id and side, or `null`; what the dragged wire is checked against. */
export type PinOf = (node: string, pin: string | null, side: PinSide) => LinkEnd | null;

/**
 * `graph`'s pins, each with what its node's wildcards may take: its kind's types and the type it notes. A kind that
 * changes a variable takes its variable's, already its pins', so its wildcards are never open (`bot::pins`).
 */
export function pinFinder(graph: GraphView_Serialize): PinOf {
  const byNode = new Map(graph.nodes.map((node) => [node.id, node]));
  return (node, pin, side) => {
    const drawn = byNode.get(node);
    const found = drawn?.pins.find((one) => one.id === pin && one.side === side);
    if (drawn === undefined || found === undefined) {
      return null;
    }
    const typed = drawn.options?.valueType ?? (drawn.options?.variable === undefined ? null : found.carries);
    return { node, pin: found, types: drawn.types ?? [], typed };
  };
}

/** The pins of the graph drawn, for the wire being dragged, which React Flow draws apart from the graph's props. */
export const GraphPins = createContext<PinOf>(() => null);
