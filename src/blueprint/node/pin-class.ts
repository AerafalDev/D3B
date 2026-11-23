import type { PinView_Serialize } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";

/**
 * A pin's look (the owner, 2026-11-17, from a mock-up): a triangle for the flow, a circle for a value coloured by its
 * type, a filled square for a list and a hollow one for ranges; a wildcard its node notes no type for a hollow grey
 * circle (a square for a list), drawn as its type once typed.
 */
export function pinClass(pin: PinView_Serialize, open: boolean): string {
  return cn(
    "blueprint-pin",
    pin.carries === "exec" ? "blueprint-exec" : "blueprint-value",
    `blueprint-type-${pin.carries}`,
    pin.list === true && "blueprint-list",
    pin.ranges === true && "blueprint-ranges",
    (pin.carries === "wildcard" || (open && pin.wildcard === true)) && "blueprint-open",
  );
}
