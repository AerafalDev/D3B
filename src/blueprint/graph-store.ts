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
  /** Undoes the last change to the blueprint `id`, in any of its graphs, written by Rust (the owner, 2026-11-16). */
  undo: (id: string) => Promise<boolean>;
  /** Redoes the last change undone, written by Rust. */
  redo: (id: string) => Promise<boolean>;
  /** Drops a blueprint deleted. */
  forget: (id: string) => void;
}

// Each blueprint's changes (an edit, an undo, a redo) one after another: Rust runs each command on a thread of its own,
// so two in flight could be applied in either order, and the editor would show another state than the file's.
const queues: Record<string, Promise<unknown>> = {};

export const useGraphs = create<GraphStore>((set, get) => {
  /** Makes a change Rust writes and takes the graphs it gives back; refused, says why and reads them again. */
  const apply = async (id: string, made: () => Promise<BlueprintGraphs>) => {
    try {
      const graphs = await made();
      set((store) => ({ read: { ...store.read, [id]: { state: "read", graphs } } }));
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
  };
  const change = (id: string, made: () => Promise<BlueprintGraphs>) => {
    const done = (queues[id] ?? Promise.resolve()).then(() => apply(id, made));
    queues[id] = done;
    return done;
  };

  return {
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

    edit: (id, graph, edit) => change(id, () => commands.editBlueprint(id, graph, edit)),
    undo: (id) => change(id, () => commands.undoBlueprint(id)),
    redo: (id) => change(id, () => commands.redoBlueprint(id)),

    forget: (id) => {
      set((store) => {
        const { [id]: _gone, ...read } = store.read;
        return { read };
      });
    },
  };
});
