/** The id of the graph's drop target, which a variable of My Blueprint is dragged onto (step 3's (14)). */
export const GRAPH_DROP = "blueprint-graph";

/** What a variable dragged from My Blueprint carries. */
export interface VariableDrag {
  variable: string;
}

/** The keys held when it is dropped. */
interface Held {
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

/** Where a drag ended on the screen: the pointer's place when it began, moved by how far it went; none without one. */
export function dropPoint(began: Event | null, moved: { x: number; y: number }): { x: number; y: number } | null {
  if (began === null || !("clientX" in began) || !("clientY" in began)) {
    return null;
  }
  const { clientX, clientY } = began as { clientX: unknown; clientY: unknown };
  if (typeof clientX !== "number" || typeof clientY !== "number") {
    return null;
  }
  return { x: clientX + moved.x, y: clientY + moved.y };
}

/** The variable a drag carries, or none for anything else dragged. */
export function draggedVariable(data: unknown): string | null {
  const variable = (data as Partial<VariableDrag> | null | undefined)?.variable;
  return typeof variable === "string" && variable !== "" ? variable : null;
}
