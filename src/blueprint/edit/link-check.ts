import type { PinView_Serialize } from "@/bindings.ts";

/** Why a wire may not join two pins, said while it is dragged (`blueprint.refused.<why>`). */
export type LinkRefusal = "sameNode" | "wrongWay" | "otherType";

/** One end of a wire: a node and its pin, and what its wildcards may take (`ipc::graph`'s `types` and `options`). */
export interface LinkEnd {
  node: string;
  pin: PinView_Serialize;
  /** The types the node's wildcards may take, its kind's; none for any. */
  types?: readonly string[];
  /** The type the node notes for its wildcards, `null` or absent while none. */
  typed?: string | null;
}

/** What a pin carries whole: its base type (`exec`, `wildcard`, `any` or a value's) and its shape. */
interface Carried {
  base: string;
  list: boolean;
  ranges: boolean;
}

function carried(end: LinkEnd): Carried {
  return { base: end.pin.carries, list: end.pin.list === true, ranges: end.pin.ranges === true };
}

/** A value of a type: neither the flow, nor a wildcard of no type, nor a Text's hole. */
function isValue({ base }: Carried): boolean {
  return base !== "exec" && base !== "wildcard" && base !== "any";
}

const sameShape = (a: Carried, b: Carried) => a.list === b.list && a.ranges === b.ranges;

/**
 * Whether a wire may carry `out` into `input` as their pins read, as `bot::Carries::feeds` decides: the flow into the
 * flow, a value into its type and shape, a percentage into a number (the owner, 2026-11-13), a list of items into the
 * items actions take, any value into a Text's hole; a wildcard of no type carries nothing.
 */
function feeds(out: Carried, input: Carried): boolean {
  if (out.base === "exec" || input.base === "exec") {
    return out.base === input.base;
  }
  if (input.base === "any") {
    return isValue(out);
  }
  if (!isValue(out) || !isValue(input)) {
    return false;
  }
  const items =
    out.base === "item" && out.list && !out.ranges && input.base === "items" && !input.list && !input.ranges;
  const percentage = out.base === "percentage" && input.base === "number";
  return items || (sameShape(out, input) && (out.base === input.base || percentage));
}

/**
 * Whether `end` is a wildcard still open to a type: one of its kind's whose node notes none, though it may read its
 * kind's first type meanwhile (`≥`'s numbers). Rust also closes one another wire or value reads so; it refuses it.
 */
function isOpen(end: LinkEnd): boolean {
  return end.pin.carries === "wildcard" || (end.pin.wildcard === true && (end.typed ?? null) === null);
}

/** Whether `open`'s wildcard may take `other`'s type, as `bot::Carries::types` and the kind's types decide. */
function takes(open: LinkEnd, other: LinkEnd): boolean {
  const [wildcard, value] = [carried(open), carried(other)];
  const types = open.types ?? [];
  return isValue(value) && sameShape(wildcard, value) && (types.length === 0 || types.includes(value.base));
}

/** Why a wire from `from`, the pin the drag began on, to `to` is refused, or `null` when it holds. */
export function linkRefusal(from: LinkEnd, to: LinkEnd): LinkRefusal | null {
  if (from.node === to.node) {
    return "sameNode";
  }
  if (from.pin.side === to.pin.side) {
    return "wrongWay";
  }
  const [out, input] = from.pin.side === "out" ? [from, to] : [to, from];
  // A wire that feeds as the pins read wins; else a wildcard still open takes the other end's type.
  if (feeds(carried(out), carried(input))) {
    return null;
  }
  const [openOut, openIn] = [isOpen(out), isOpen(input)];
  if (openOut !== openIn && (openOut ? takes(out, input) : takes(input, out))) {
    return null;
  }
  return "otherType";
}
