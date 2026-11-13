import { create } from "zustand";

import { type AppInfo, commands } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";

interface AppInfoStore {
  /** The app's version and its game data, once the back end told them; `null` before, and in the browser preview. */
  info: AppInfo | null;
  /** Asks the back end once; a failed answer leaves it unknown, to ask again at the next call. */
  load: () => Promise<void>;
}

// The question under way, so that two callers at start ask once.
let asking: Promise<void> | null = null;

export const useAppInfo = create<AppInfoStore>((set, get) => ({
  info: null,

  load: () => {
    if (get().info !== null || !isTauri()) {
      return Promise.resolve();
    }
    asking ??= commands
      .appInfo()
      .then((info) => set({ info }))
      // Nothing to show until it answers: the bar keeps the app's name alone.
      .catch(() => {})
      .finally(() => {
        asking = null;
      });
    return asking;
  },
}));
