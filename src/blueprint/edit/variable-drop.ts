import type { GraphEdit_Deserialize } from "@/bindings.ts";

/** The id of the graph's drop target, which a variable of My Blueprint is dragged onto (step 3's (14)). */
export const GRAPH_DROP = "blueprint-graph";

/** What a variable dragged from My Blueprint carries. */
export interface VariableDrag {
  variable: string;
}

/** The keys held when it is dropped. */
export interface Held {
  ctrlKey: boolean;
  metaKey: boolean;
  altKey: boolean;
}

/**
 * What a variable dropped on the graph makes (the owner's choice of 2026-11-17, Unreal's): Ctrl (Cmd on macOS) reads
 * it, Alt writes it, and otherwise a small menu at the pointer asks which.
 */
export function droppedAs({ ctrlKey, metaKey, altKey }: Held): "get" | "set" | "ask" {
  const read = ctrlKey || metaKey;
  if (read === altKey) {
    return "ask";
  }
  return read ? "get" : "set";
}

/** The variable a drag carries, or none for anything else dragged. */
export function draggedVariable(data: unknown): string | null {
  const variable = (data as Partial<VariableDrag> | null | undefined)?.variable;
  return typeof variable === "string" && variable !== "" ? variable : null;
}

/** The pointer as it last moved over the window: where, and the keys held. */
export interface Pointer extends Held {
  x: number;
  y: number;
}

/** A variable dropped on the graph: a node added at once, or the menu asking where it was dropped. */
export type Dropped =
  | { kind: "add"; edit: GraphEdit_Deserialize }
  | { kind: "ask"; variable: string; client: { x: number; y: number }; at: { x: number; y: number } };

/**
 * What a drag ending makes in the graph (the owner's choice of 2026-11-17): nothing when it carries no variable, ends
 * off the graph or the graph is not mine; else, at the pointer's last place (`toFlow` the graph's point under it), a
 * node reading or writing it, or the menu asking which.
 */
export function variableDropped(
  data: unknown,
  overGraph: boolean,
  editable: boolean,
  pointer: Pointer | null,
  toFlow: (client: { x: number; y: number }) => { x: number; y: number },
): Dropped | null {
  const variable = draggedVariable(data);
  if (variable === null || !overGraph || !editable || pointer === null) {
    return null;
  }
  const client = { x: pointer.x, y: pointer.y };
  const at = toFlow(client);
  const does = droppedAs(pointer);
  if (does === "ask") {
    return { kind: "ask", variable, client, at };
  }
  const position: [number, number] = [Math.round(at.x), Math.round(at.y)];
  return { kind: "add", edit: { edit: "add", node: { does, variable }, position, wiredTo: null } };
}
