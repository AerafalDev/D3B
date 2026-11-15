import { create } from "zustand";

import { type Atlas_Serialize, type AtlasError, commands, type HintKind } from "@/bindings.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { HIDDEN_KINDS } from "./hint-layout.ts";

/** The atlas as read: loading, read, or why not. */
export type AtlasRead =
  | { state: "loading" }
  | { state: "read"; atlas: Atlas_Serialize }
  | { state: "failed"; error: AtlasError };

/** A Zone being chosen on the map (the owner's mock-up 12, 2026-11-15). */
export interface Choosing {
  /** Whose Zone, as the map's banner says. */
  who: string;
  /** Takes the sub-area chosen. */
  choose: (subArea: number) => void;
  /** The editor whose tab was active when the choice began, brought back when it ends. */
  back: string | null;
}

/** The world the tab opens on: the Twelve's, the client's first. */
const FIRST_WORLD = 1;

interface WorldStore {
  /** The atlas, read once; `null` before. */
  read: AtlasRead | null;
  /** The sub-area chosen in the view, shown on the map. */
  picked: number | null;
  /** The world the tab shows. */
  world: number;
  /** The kinds of hints the map leaves out, for the session. */
  hidden: ReadonlySet<HintKind>;
  /** The Zone being chosen on the map, `null` when none is. */
  choosing: Choosing | null;
  /** Reads the atlas, once. */
  load: () => Promise<void>;
  /** Chooses a sub-area, and shows the world that holds its maps. */
  pick: (subArea: number) => void;
  /** Shows a world. */
  show: (world: number) => void;
  /** Sets the kinds of hints the map leaves out. */
  hide: (kinds: ReadonlySet<HintKind>) => void;
  /** Begins choosing `who`'s Zone on the map, remembering the tab active now. */
  startChoosing: (who: string, choose: (subArea: number) => void) => void;
  /** Ends the choice with the sub-area chosen, also shown on the map, or `null` when cancelled; the tab it began in
   * comes back. */
  endChoosing: (subArea: number | null) => void;
}

export const useWorld = create<WorldStore>((set, get) => ({
  read: null,
  picked: null,
  world: FIRST_WORLD,
  hidden: HIDDEN_KINDS,
  choosing: null,

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
    // The world asked for when the atlas has it, else the atlas's first.
    const { world } = get();
    const worlds = read.state === "read" ? read.atlas.worlds : [];
    set({ read, world: worlds.some((one) => one.id === world) ? world : (worlds[0]?.id ?? world) });
  },

  pick: (subArea) => {
    const read = get().read;
    const held = read?.state === "read" ? findSubArea(read.atlas, subArea) : undefined;
    // A sub-area holds maps on one world at most (`ipc::atlas`): the tab goes there.
    const world = held?.maps[0]?.world;
    set(world === undefined ? { picked: subArea } : { picked: subArea, world });
  },

  show: (world) => set({ world }),

  hide: (hidden) => set({ hidden }),

  startChoosing: (who, choose) => set({ choosing: { who, choose, back: useEditors.getState().active } }),

  endChoosing: (subArea) => {
    const { choosing, pick } = get();
    set({ choosing: null });
    if (subArea !== null) {
      pick(subArea);
      choosing?.choose(subArea);
    }
    const back = choosing?.back;
    if (back !== null && back !== undefined) {
      useEditors.getState().api?.getPanel(back)?.api.setActive();
    }
  },
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
