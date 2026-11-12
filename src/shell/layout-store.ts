import { create } from "zustand";

/** The parts of the shell the player shows or hides, as in VS Code. */
export type Part = "primarySideBar" | "panel" | "secondarySideBar";

interface LayoutStore {
  /** Which parts are shown. As in VS Code, the secondary side bar starts hidden (the owner, 2026-11-12). */
  visible: Record<Part, boolean>;
  /** Whether the command palette is open. */
  paletteOpen: boolean;
  toggle: (part: Part) => void;
  setVisible: (part: Part, visible: boolean) => void;
  setPaletteOpen: (open: boolean) => void;
}

export const useLayout = create<LayoutStore>((set) => ({
  visible: { primarySideBar: true, panel: true, secondarySideBar: false },
  paletteOpen: false,
  toggle: (part) => set((store) => ({ visible: { ...store.visible, [part]: !store.visible[part] } })),
  setVisible: (part, visible) =>
    set((store) => (store.visible[part] === visible ? store : { visible: { ...store.visible, [part]: visible } })),
  setPaletteOpen: (paletteOpen) => set({ paletteOpen }),
}));
