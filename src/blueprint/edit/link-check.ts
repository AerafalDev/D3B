import type { PinCarries, PinView_Serialize } from "@/bindings.ts";

/** Why a wire may not join two pins, said while it is dragged (`blueprint.refused.<why>`). */
export type LinkRefusal = "sameNode" | "wrongWay" | "otherType";

/** One end of a wire: a node and its pin. */
export interface LinkEnd {
  node: string;
  pin: PinView_Serialize;
}

/**
 * Whether a wire may carry `out` into `input`: the flow into the flow, a value into its type, and a percentage into a
 * number (the owner, 2026-11-13), as `bot::Carries::feeds` decides when the wire is saved.
 */
function feeds(out: PinCarries, input: PinCarries): boolean {
  return out === input || (out === "percentage" && input === "number");
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
  return feeds(out.pin.carries, input.pin.carries) ? null : "otherType";
}
