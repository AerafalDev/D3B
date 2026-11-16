import { BaseEdge, type EdgeProps, type InternalNode, useInternalNode } from "@xyflow/react";

import { wirePath } from "./graph-look.ts";

/** A node's lowest edge in the graph, or its top while it is not measured yet. */
function bottomOf(node: InternalNode | undefined): number {
  return node === undefined ? 0 : node.internals.positionAbsolute.y + (node.measured.height ?? 0);
}

/** A wire of the graph: a curve forward, a loop under its two nodes when it goes back (`wirePath`). */
export function WireEdge({ id, source, target, sourceX, sourceY, targetX, targetY }: EdgeProps) {
  const bottom = Math.max(bottomOf(useInternalNode(source)), bottomOf(useInternalNode(target)));
  return <BaseEdge id={id} path={wirePath({ x: sourceX, y: sourceY }, { x: targetX, y: targetY }, bottom)} />;
}
