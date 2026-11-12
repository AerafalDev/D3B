import type { DockviewApi } from "dockview-react";
import { create } from "zustand";

import type { IconName } from "@/icons/icons.ts";

interface EditorStore {
  /** Dockview's API once the editor area is mounted; `null` before. */
  api: DockviewApi | null;
  setApi: (api: DockviewApi | null) => void;
}

export const useEditors = create<EditorStore>((set) => ({
  api: null,
  setApi: (api) => set({ api }),
}));

/** What an editor tab carries: its icon in the tab, and the editor's own parameters. */
export interface EditorParams {
  icon: IconName;
  [key: string]: unknown;
}

interface OpenEditor {
  /** One tab per id: opening an id already open brings its tab forward. */
  id: string;
  /** The editor's component, as `EditorArea` names it. */
  component: string;
  title: string;
  params: EditorParams;
}

/** Opens an editor in the editor area, or brings its tab forward when it is already open, as VS Code does. */
export function openEditor({ id, component, title, params }: OpenEditor): void {
  const api = useEditors.getState().api;
  if (api === null) {
    return;
  }
  const open = api.getPanel(id);
  if (open === undefined) {
    api.addPanel({ id, component, title, params });
  } else {
    open.api.setActive();
  }
}
