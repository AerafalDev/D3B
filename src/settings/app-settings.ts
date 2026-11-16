import i18next from "i18next";
import { toast } from "sonner";
import { create } from "zustand";

import { commands, type Settings, type SettingsError } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";
import { useLayout } from "@/shell/layout-store.ts";

interface AppSettingsStore extends Settings {
  /** Whether the player changed a setting this session: a read at start that ends after it is dropped. */
  changed: boolean;
  /** Reads the settings of the player's folder, once at start; the defaults in the browser preview. */
  load: () => Promise<void>;
  /**
   * Shows or hides the Protocol view and its panel, the panel's only view until step 4's History; saved at once, and
   * kept for the session when it cannot be.
   */
  setAdvancedTools: (on: boolean) => void;
}

/** What a failed save says, in the player's language. */
function failure(error: unknown): string {
  // The command rejects with its `SettingsError`; anything else is a bug, said as it came.
  const failed = error as SettingsError;
  if (failed?.kind === "notSaved") {
    return i18next.t("appSettings.notSaved", { reason: failed.reason });
  }
  if (failed?.kind === "noFolder") {
    return i18next.t("appSettings.notSaved", { reason: i18next.t("appSettings.noFolder") });
  }
  return String(error);
}

/** The app's settings (`docs/shell.md`, "Settings"): `settings.json` in the player's folder. */
export const useAppSettings = create<AppSettingsStore>((set, get) => ({
  advancedTools: false,
  changed: false,

  load: async () => {
    if (!isTauri()) {
      return;
    }
    try {
      const settings = await commands.appSettings();
      if (!get().changed) {
        set(settings);
        useLayout.getState().setVisible("panel", settings.advancedTools);
      }
    } catch (error) {
      console.error("the app's settings could not be read", error);
    }
  },

  setAdvancedTools: (advancedTools) => {
    set({ advancedTools, changed: true });
    useLayout.getState().setVisible("panel", advancedTools);
    if (isTauri()) {
      // The file is written whole: every setting, today this one.
      commands.setAppSettings({ advancedTools }).catch((error: unknown) => toast.error(failure(error)));
    }
  },
}));
