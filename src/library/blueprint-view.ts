import { create } from "zustand";

/** What a blueprint's tab shows in its centre: its graph, or its Summary. */
export type BlueprintView = "graph" | "summary";

interface BlueprintViewStore {
  /** Each blueprint tab's view, by its tab's id; the graph when unset. */
  view: Record<string, BlueprintView>;
  setView: (tab: string, view: BlueprintView) => void;
}

/**
 * The views of the blueprints' tabs, shared by the tab and its toggle in the tab strip (the owner, 2026-11-14), which
 * dockview draws apart from it.
 */
export const useBlueprintView = create<BlueprintViewStore>((set) => ({
  view: {},
  setView: (tab, view) => set((store) => ({ view: { ...store.view, [tab]: view } })),
}));
