import { create } from "zustand";

import { type CharacterView_Serialize, events } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";

/** A character's view as the back end sends it (`docs/game.md`, "In the app"). */
export type CharacterView = CharacterView_Serialize;

interface CharacterStore {
  /** Each game connection's character, by the connection's number, until it closes. */
  views: Record<number, CharacterView>;
  /** The connection whose character fills the workspace. */
  selected: number | null;
  select: (connection: number | null) => void;
  /** Follows the characters from the back end; returns what stops following. */
  follow: () => () => void;
}

export const useCharacters = create<CharacterStore>((set) => ({
  views: {},
  selected: null,
  select: (selected) => set({ selected }),

  follow: () => {
    if (!isTauri()) {
      return () => {};
    }
    let live = true;
    const unlisten = events.characterChanged.listen((event) => {
      if (!live) {
        return;
      }
      const { connection, view } = event.payload;
      set((store) => {
        const { [connection]: _gone, ...others } = store.views;
        if (view !== null) {
          return { views: { ...others, [connection]: view } };
        }
        return { views: others, selected: store.selected === connection ? null : store.selected };
      });
    });
    return () => {
      live = false;
      void unlisten.then((stop) => stop());
    };
  },
}));
