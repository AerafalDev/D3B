import { create } from "zustand";

import { type Atlas_Serialize, type AtlasError, commands } from "@/bindings.ts";

/** The atlas as read: loading, read, or why not. */
export type AtlasRead =
  | { state: "loading" }
  | { state: "read"; atlas: Atlas_Serialize }
  | { state: "failed"; error: AtlasError };

/** The world the tab opens on: the Twelve's, the client's first. */
const FIRST_WORLD = 1;

interface WorldStore {
  /** The atlas, read once; `null` before. */
  read: AtlasRead | null;
  /** The sub-area chosen in the view, shown on the map. */
  picked: number | null;
  /** The world the tab shows. */
  world: number;
  /** Reads the atlas, once. */
  load: () => Promise<void>;
  /** Chooses a sub-area, and shows the world that holds its maps. */
  pick: (subArea: number) => void;
  /** Shows a world. */
  show: (world: number) => void;
}

export const useWorld = create<WorldStore>((set, get) => ({
  read: null,
  picked: null,
  world: FIRST_WORLD,

  load: async () => {
    if (get().read !== null) {
      return;
    }
    set({ read: { state: "loading" } });
    let read: AtlasRead;
    try {
      read = { state: "read", atlas: await commands.worldAtlas() };
    } catch (error) {
      // The command rejects with its `AtlasError`; anything else is a bug, said as it came.
      const failure = error as AtlasError;
      read = {
        state: "failed",
        error: typeof failure?.kind === "string" ? failure : { kind: "noData", reason: String(error) },
      };
    }
    const atlas = read.state === "read" ? read.atlas : null;
    const world = get().world;
    // The first world when the atlas has it, else its first.
    const known = atlas === null || atlas.worlds.some((one) => one.id === world);
    set({ read, world: known ? world : (atlas.worlds[0]?.id ?? world) });
  },

  pick: (subArea) => {
    const read = get().read;
    const held = read?.state === "read" ? findSubArea(read.atlas, subArea) : undefined;
    // A sub-area holds maps on one world at most (`ipc::atlas`): the tab goes there.
    const world = held?.maps[0]?.world;
    set(world === undefined ? { picked: subArea } : { picked: subArea, world });
  },

  show: (world) => set({ world }),
}));

/** A sub-area of `atlas` and its area, by id. */
export function findSubArea(atlas: Atlas_Serialize, id: number) {
  for (const area of atlas.areas) {
    const subArea = area.subAreas.find((one) => one.id === id);
    if (subArea !== undefined) {
      return { ...subArea, area };
    }
  }
  return undefined;
}
