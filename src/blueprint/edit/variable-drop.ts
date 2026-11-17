/** The type of the drag a variable of My Blueprint makes, its id as the data (step 3's (14)). */
export const VARIABLE_DRAG = "application/x-d3b-variable";

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
