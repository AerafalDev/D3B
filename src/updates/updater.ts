import { exit, relaunch } from "@tauri-apps/plugin-process";
import { check, type Update } from "@tauri-apps/plugin-updater";
import { create } from "zustand";

/** Where the update of the running app stands. */
export type UpdateState =
  | { status: "idle" }
  | { status: "checking" }
  | { status: "downloading"; version: string }
  | { status: "ready"; version: string; notes: string | undefined }
  | { status: "installing"; version: string }
  | { status: "failed"; error: string };

interface UpdaterStore {
  state: UpdateState;
  /** Checks the channel and downloads a newer version in the background; does nothing while one is under way. */
  checkAndDownload: () => Promise<void>;
  /** Installs the downloaded version and starts it. */
  restart: () => Promise<void>;
  /** Installs the downloaded version, if any, as the app closes. Returns whether it did. */
  installOnClose: () => Promise<boolean>;
}

// The Update object holds a handle in the back end; it stays outside the store, which keeps plain data only.
let downloaded: Update | null = null;

function message(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

export const useUpdater = create<UpdaterStore>((set, get) => ({
  state: { status: "idle" },

  checkAndDownload: async () => {
    const { status } = get().state;
    if (status !== "idle" && status !== "failed") {
      return;
    }
    set({ state: { status: "checking" } });
    try {
      const update = await check();
      downloaded = null;
      if (update === null) {
        set({ state: { status: "idle" } });
        return;
      }
      set({ state: { status: "downloading", version: update.version } });
      await update.download();
      downloaded = update;
      set({ state: { status: "ready", version: update.version, notes: update.body } });
    } catch (error) {
      set({ state: { status: "failed", error: message(error) } });
    }
  },

  restart: async () => {
    const update = downloaded;
    if (update === null) {
      return;
    }
    set({ state: { status: "installing", version: update.version } });
    try {
      await update.install();
      downloaded = null;
      await relaunch();
    } catch (error) {
      set({ state: { status: "failed", error: message(error) } });
    }
  },

  installOnClose: async () => {
    const update = downloaded;
    if (update === null) {
      return false;
    }
    downloaded = null;
    try {
      await update.install();
    } catch {
      // The app is closing: a failed install is retried at the next start, which downloads it again.
    }
    await exit(0);
    return true;
  },
}));
