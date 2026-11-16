import i18next from "i18next";
import { toast } from "sonner";
import { create } from "zustand";

import {
  type BlueprintGraphs_Serialize,
  commands,
  type EditError,
  type GraphEdit_Deserialize,
  type GraphsError,
} from "@/bindings.ts";

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
  /**
   * Makes a change to the graph `graph` of the blueprint `id`, saved at once (the owner, 2026-11-16), and takes the
   * graphs it gives back; refused, says why and reads it again, so the editor shows the file. Whether it was saved.
   */
  edit: (id: string, graph: string, edit: GraphEdit_Deserialize) => Promise<boolean>;
}

// Each edit is numbered by blueprint: an older one that ends after a newer one is dropped.
const edits: Record<string, number> = {};

export const useGraphs = create<GraphStore>((set, get) => ({
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

  edit: async (id, graph, edit) => {
    const number = (edits[id] ?? 0) + 1;
    edits[id] = number;
    try {
      const graphs = await commands.editBlueprint(id, graph, edit);
      if (number === edits[id]) {
        set((store) => ({ read: { ...store.read, [id]: { state: "read", graphs } } }));
      }
      return true;
    } catch (error) {
      // The editor offers no refused edit: one says the file changed under it, or could not be written.
      const failure = error as EditError;
      toast.error(
        failure?.kind === "notSaved"
          ? i18next.t("blueprint.notSaved", { reason: failure.reason })
          : i18next.t("blueprint.editRefused"),
      );
      await get().load(id);
      return false;
    }
  },
}));
