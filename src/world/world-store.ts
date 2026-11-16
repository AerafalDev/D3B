import { create } from "zustand";

import { type Atlas_Serialize, type AtlasError, commands, type HintKind } from "@/bindings.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { HIDDEN_KINDS } from "./hint-layout.ts";
import { WORLD_EDITOR } from "./open-world.ts";

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
  /** Stops watching for the World tab's closing, which cancels the choice. */
  unwatch: () => void;
}

/** A tool of the World tab's map while a zone of mine is open (the owner, 2026-11-16). */
export type Tool = "select" | "brush" | "eraser" | "subArea";

/** The world the tab opens on: the Twelve's, the client's first. */
const FIRST_WORLD = 1;

interface WorldStore {
  /** The atlas, read once; `null` before. */
  read: AtlasRead | null;
  /** The sub-area chosen in the view, shown on the map. */
  picked: number | null;
  /** The zone of mine open on the map, painted with `tool`; `null` when none is. */
  zone: string | null;
  /** The map's tool while a zone of mine is open. */
  tool: Tool;
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
  /** Opens a zone of mine on its world, with `tool`; a sub-area chosen is no longer. */
  openZone: (zone: string, world: number, tool?: Tool) => void;
  /** Takes another tool. */
  takeTool: (tool: Tool) => void;
  /** Shows a world, closing a zone of mine open on another. */
  show: (world: number) => void;
  /** Sets the kinds of hints the map leaves out. */
  hide: (kinds: ReadonlySet<HintKind>) => void;
  /** Begins choosing `who`'s Zone on the map, remembering the tab active now; closing the World tab cancels it. */
  startChoosing: (who: string, choose: (subArea: number) => void) => void;
  /** Ends the choice with the sub-area chosen, also shown on the map, or `null` when cancelled; the tab it began in
   * comes back. */
  endChoosing: (subArea: number | null) => void;
}

export const useWorld = create<WorldStore>((set, get) => ({
  read: null,
  picked: null,
  zone: null,
  tool: "brush",
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
    set(world === undefined ? { picked: subArea, zone: null } : { picked: subArea, zone: null, world });
  },

  openZone: (zone, world, tool) => set({ zone, world, picked: null, tool: tool ?? get().tool }),

  takeTool: (tool) => set({ tool }),

  // A zone of mine is on one world: another world closes it.
  show: (world) => set(world === get().world ? {} : { world, zone: null }),

  hide: (hidden) => set({ hidden }),

  startChoosing: (who, choose) => {
    get().choosing?.unwatch();
    const { api, active } = useEditors.getState();
    // A choice left by closing the World tab must not wait for its next opening.
    const closing = api?.onDidRemovePanel((panel) => {
      if (panel.id === WORLD_EDITOR) {
        get().endChoosing(null);
      }
    });
    set({ choosing: { who, choose, back: active, unwatch: () => closing?.dispose() } });
  },

  endChoosing: (subArea) => {
    const { choosing, pick } = get();
    choosing?.unwatch();
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
