import type { GraphNode } from "../node-sentence.ts";

/**
 * How the Summary reads a flow output that does not go on (`docs/bot.md`, "The flow's kinds (v2.3)", the owner's
 * mock-up of 2026-11-17): a branch under its title ("Si ça échoue :"), sub-steps with none (a Sequence's outputs but
 * its last), or one of the outputs said on a line under their step ("Bûcheron → 2.1 · Sinon → 2.3").
 */
export type PinRole = "titled" | "untitled" | "listed";

/** The outputs a step goes on by, the first of them its node has. */
const ON = ["then", "done"];

/** The outputs said on one line under their step, by kind of pin, beyond a Switch's cases. */
const LISTED = new Set(["otherwise"]);

function numbered(pin: string, prefix: string): boolean {
  return new RegExp(`^${prefix}_[1-9][0-9]*$`).test(pin);
}

/** The flow outputs of `node`, in its pins' order. */
function outs(node: GraphNode): string[] {
  return node.pins.filter((pin) => pin.side === "out" && pin.carries === "exec").map((pin) => pin.id);
}

/** The output `node`'s step goes on by: Then or Done, or a Sequence's last, the rest of its flow; none for an If. */
export function onPin(node: GraphNode): string | undefined {
  const all = outs(node);
  const prefix = node.adds?.pin;
  if (prefix !== undefined && node.adds?.side === "out") {
    return all.filter((pin) => numbered(pin, prefix)).at(-1);
  }
  return all.find((pin) => ON.includes(pin));
}

/** How the Summary reads `pin`, an output of `node` other than the one it goes on by. */
export function pinRole(node: GraphNode, pin: string): PinRole {
  const prefix = node.adds?.pin;
  if (prefix !== undefined && numbered(pin, prefix)) {
    return "untitled";
  }
  if (node.takesCases === true && (numbered(pin, "case") || LISTED.has(pin))) {
    return "listed";
  }
  return "titled";
}
