import { createContext } from "react";

import type { PinSide } from "@/bindings.ts";
import type { LinkEnd } from "./link-check.ts";

/** A graph's pin by its node, id and side, or `null`; what the dragged wire is checked against. */
export type PinOf = (node: string, pin: string | null, side: PinSide) => LinkEnd | null;

/** The pins of the graph drawn, for the wire being dragged, which React Flow draws apart from the graph's props. */
export const GraphPins = createContext<PinOf>(() => null);
