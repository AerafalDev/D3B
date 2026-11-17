import type { GraphView_Serialize } from "@/bindings.ts";
import type { GraphNode } from "../node-sentence.ts";
import { onPin, type PinRole, pinRole } from "./summary-pins.ts";

/** A step of the Summary: its number ("3", or "3.1" in a branch), its node, and the branches it opens. */
export interface SummaryStep {
  label: string;
  node: GraphNode;
  branches: SummaryBranch[];
}

/** A branch a step opens: its Failed, an If's true or false, a Sequence's output, a Switch's case; read by its role. */
export interface SummaryBranch {
  pin: string;
  role: PinRole;
  flow: SummaryFlow;
}

/** Where a flow goes after its steps: to a step already said, back or forward, or nowhere. */
export type SummaryJump = { to: string; back: boolean } | null;

/** A flow read as steps, then where it goes. */
export interface SummaryFlow {
  steps: SummaryStep[];
  jump: SummaryJump;
}

/** "3.1" against "5", segment by segment: negative when `a` comes first. */
function order(a: string, b: string): number {
  const left = a.split(".").map(Number);
  const right = b.split(".").map(Number);
  for (let index = 0; index < Math.max(left.length, right.length); index += 1) {
    const difference = (left[index] ?? 0) - (right[index] ?? 0);
    if (difference !== 0) {
      return difference;
    }
  }
  return 0;
}

/**
 * The flow `graph` runs from `node`'s output `pin`, as numbered steps (`docs/bot.md`, "In the app" and "The flow's kinds
 * (v2.3)"): each node once, its outputs but the one it goes on by as branches numbered under it, counting on across
 * them, and a node met again said as a jump to its number, a loop's "back to 2". The steps go on first, so a branch
 * that joins them later jumps to them.
 */
export function summaryFlow(graph: GraphView_Serialize, node: string, pin: string): SummaryFlow {
  const byId = new Map(graph.nodes.map((one) => [one.id, one]));
  const next = (from: string, out: string) => {
    const wire = graph.wires.find((one) => one.from[0] === from && one.from[1] === out);
    return wire === undefined ? undefined : byId.get(wire.to[0]);
  };
  const numbered = new Map<string, string>();

  // A chain of steps from `first`, numbered under `prefix` from `start`; its branches are read once every chain above
  // them is.
  const chain = (first: GraphNode | undefined, prefix: string, from: string, start = 1): SummaryFlow => {
    const steps: SummaryStep[] = [];
    let current = first;
    let at = from;
    while (current !== undefined) {
      const known = numbered.get(current.id);
      if (known !== undefined) {
        return { steps, jump: { to: known, back: order(known, at) <= 0 } };
      }
      const label = `${prefix}${start + steps.length}`;
      numbered.set(current.id, label);
      steps.push({ label, node: current, branches: [] });
      at = label;
      const on = onPin(current);
      current = on === undefined ? undefined : next(current.id, on);
    }
    return { steps, jump: null };
  };
  const branch = (flow: SummaryFlow) => {
    for (const step of flow.steps) {
      // A node's other outputs, in its pins' order, count on from one another, an If's True 2.1 and 2.2 then its
      // False 2.3, so a jump names one step; a Switch's case wired to nothing is said, leading nowhere.
      let start = 1;
      const on = onPin(step.node);
      const outs = step.node.pins.filter((one) => one.side === "out" && one.carries === "exec" && one.id !== on);
      for (const { id: out } of outs) {
        const role = pinRole(step.node, out);
        const target = next(step.node.id, out);
        if (target !== undefined || role === "listed") {
          const opened = chain(target, `${step.label}.`, step.label, start);
          start += opened.steps.length;
          step.branches.push({ pin: out, role, flow: opened });
        }
      }
      for (const opened of step.branches) {
        branch(opened.flow);
      }
    }
  };
  const flow = chain(next(node, pin), "", "0");
  branch(flow);
  return flow;
}
