import { create } from "zustand";

import { type BlueprintGraphs_Serialize, commands, type GraphsError } from "@/bindings.ts";

/** A blueprint's graphs as the editor draws them (`ipc::graph`). */
export type BlueprintGraphs = BlueprintGraphs_Serialize;

/** A blueprint's graphs as read: loading, read, or why not. */
export type GraphsRead =
  | { state: "loading" }
  | { state: "read"; graphs: BlueprintGraphs }
  | { state: "failed"; error: GraphsError };

interface GraphStore {
  /** Each blueprint's graphs read, by id. */
  read: Record<string, GraphsRead>;
  /** Reads a blueprint's graphs again. */
  load: (id: string) => Promise<void>;
}

export const useGraphs = create<GraphStore>((set) => ({
  read: {},

  load: async (id) => {
    set((store) => ({ read: { ...store.read, [id]: store.read[id] ?? { state: "loading" } } }));
    let read: GraphsRead;
    try {
      read = { state: "read", graphs: await commands.blueprintGraphs(id) };
    } catch (error) {
      // The command rejects with its `GraphsError`; anything else is a bug, said as it came.
      const failure = error as GraphsError;
      read = {
        state: "failed",
        error: typeof failure?.kind === "string" ? failure : { kind: "bug", reason: String(error) },
      };
    }
    set((store) => ({ read: { ...store.read, [id]: read } }));
  },
}));
