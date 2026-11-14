import type { GraphView_Serialize } from "@/bindings.ts";
import type { GraphNode } from "../node-sentence.ts";

/** A step of the Summary: its number ("3", or "3.1" in a branch), its node, and the branches it opens. */
export interface SummaryStep {
  label: string;
  node: GraphNode;
  branches: SummaryBranch[];
}

/** A branch a step opens: on its Failed, or an If's true or false. */
export interface SummaryBranch {
  pin: BranchPin;
  flow: SummaryFlow;
}

/** Where a flow goes after its steps: to a step already said, back or forward, or nowhere. */
export type SummaryJump = { to: string; back: boolean } | null;

/** A flow read as steps, then where it goes. */
export interface SummaryFlow {
  steps: SummaryStep[];
  jump: SummaryJump;
}

/** The flow outputs that open a branch rather than go on, in the order they are said. */
const BRANCHES = ["true", "false", "failed"] as const;
type BranchPin = (typeof BRANCHES)[number];

/** The flow outputs a step goes on by. */
const ON = new Set(["then", "done"]);

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
 * The flow `graph` runs from `node`'s output `pin`, as numbered steps (`docs/bot.md`, "In the app"): each node once,
 * its Failed and an If's outputs as branches numbered under it, and a node met again said as a jump to its number, a
 * loop's "back to 2". The steps go on first, so a branch that joins them later jumps to them.
 */
export function summaryFlow(graph: GraphView_Serialize, node: string, pin: string): SummaryFlow {
  const byId = new Map(graph.nodes.map((one) => [one.id, one]));
  const next = (from: string, out: string) => {
    const wire = graph.wires.find((one) => one.from[0] === from && one.from[1] === out);
    return wire === undefined ? undefined : byId.get(wire.to[0]);
  };
  const numbered = new Map<string, string>();

  // A chain of steps from `first`, numbered under `prefix`; its branches are read once every chain above them is.
  const chain = (first: GraphNode | undefined, prefix: string, from: string): SummaryFlow => {
    const steps: SummaryStep[] = [];
    let current = first;
    let at = from;
    while (current !== undefined) {
      const known = numbered.get(current.id);
      if (known !== undefined) {
        return { steps, jump: { to: known, back: order(known, at) <= 0 } };
      }
      const label = `${prefix}${steps.length + 1}`;
      numbered.set(current.id, label);
      steps.push({ label, node: current, branches: [] });
      at = label;
      const on = current.pins.find((one) => one.side === "out" && one.carries === "exec" && ON.has(one.id));
      current = on === undefined ? undefined : next(current.id, on.id);
    }
    return { steps, jump: null };
  };
  const branch = (flow: SummaryFlow) => {
    for (const step of flow.steps) {
      for (const out of BRANCHES) {
        const target = next(step.node.id, out);
        if (target !== undefined) {
          step.branches.push({ pin: out, flow: chain(target, `${step.label}.`, step.label) });
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
