import { create } from "zustand";

/** The parts of the shell the player shows or hides, as in VS Code. */
export type Part = "primarySideBar" | "panel" | "secondarySideBar";

/** The views the activity bar opens in the primary side bar, one per feature that has one. */
export type ViewId = "characters";

interface LayoutStore {
  /** Which parts are shown. As in VS Code, the secondary side bar starts hidden (the owner, 2026-11-12). */
  visible: Record<Part, boolean>;
  /** The view the primary side bar shows. */
  view: ViewId;
  /** Whether the command palette is open. */
  paletteOpen: boolean;
  toggle: (part: Part) => void;
  setVisible: (part: Part, visible: boolean) => void;
  setPaletteOpen: (open: boolean) => void;
  /** VS Code's activity bar: shows a view in the primary side bar, or hides the side bar when that view is showing. */
  showView: (view: ViewId) => void;
}

export const useLayout = create<LayoutStore>((set) => ({
  visible: { primarySideBar: true, panel: true, secondarySideBar: false },
  view: "characters",
  paletteOpen: false,
  toggle: (part) => set((store) => ({ visible: { ...store.visible, [part]: !store.visible[part] } })),
  setVisible: (part, visible) =>
    set((store) => (store.visible[part] === visible ? store : { visible: { ...store.visible, [part]: visible } })),
  setPaletteOpen: (paletteOpen) => set({ paletteOpen }),
  showView: (view) =>
    set((store) => {
      const showing = store.visible.primarySideBar && store.view === view;
      return { view, visible: { ...store.visible, primarySideBar: !showing } };
    }),
}));
