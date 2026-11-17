import { createContext } from "react";

import type { GraphEdit_Deserialize } from "@/bindings.ts";
import type { GraphNode } from "../node-sentence.ts";

/**
 * A change to the graph a node makes itself (its "+"), given by the graph for a blueprint of mine; none for one of
 * D3B's, read-only, so no node draws a "+".
 */
export const NodeEdit = createContext<((edit: GraphEdit_Deserialize) => void) | undefined>(undefined);

/** The most numbered pins a node holds, as the file's count does (`bot::Options`). */
const MOST = 255;

/** The number of `pin` when it is `<prefix>_<n>` written as the file does, `then_3`; never `then_03`. */
function numberOf(pin: string, prefix: string): number | null {
  const match = new RegExp(`^${prefix}_([1-9][0-9]*)$`).exec(pin);
  return match?.[1] === undefined ? null : Number(match[1]);
}

/**
 * The edit "+" makes (`docs/bot.md`, "The flow's kinds (v2.3)"): one more of the pins its kind numbers, Sequence's
 * outputs, AND's and OR's inputs; `null` on a node that numbers none, or holds the most already.
 */
export function addedPin(node: GraphNode): GraphEdit_Deserialize | null {
  const adds = node.adds;
  if (adds === undefined || adds === null) {
    return null;
  }
  const count = node.options?.count ?? adds.least;
  return count >= MOST ? null : { edit: "options", node: node.id, count: count + 1 };
}

/**
 * Whether `pin` is one of the pins `node`'s options make, a numbered output or a case, whose menu is "Remove" rather
 * than the node's "Delete", though at the kind's least it removes nothing.
 */
export function optionPin(node: GraphNode, pin: string): boolean {
  const adds = node.adds;
  const counted = adds !== undefined && adds !== null && numberOf(pin, adds.pin) !== null;
  return counted || (node.takesCases === true && numberOf(pin, "case") !== null);
}

/**
 * The edit a pin's "Remove" makes: a numbered output above its kind's least, or a case, each dropping its wire, those
 * after it renumbered by Rust (the owner, 2026-11-17, as Unreal does); `null` for any other pin, which has no "Remove".
 */
export function pinRemoval(node: GraphNode, pin: string): GraphEdit_Deserialize | null {
  const removal = { edit: "removePin", node: node.id, pin } as const;
  const adds = node.adds;
  if (adds !== undefined && adds !== null) {
    const number = numberOf(pin, adds.pin);
    const count = node.options?.count ?? adds.least;
    if (number !== null && number <= count && count > adds.least) {
      return removal;
    }
  }
  if (node.takesCases === true) {
    const number = numberOf(pin, "case");
    if (number !== null && number <= (node.options?.cases ?? []).length) {
      return removal;
    }
  }
  return null;
}
