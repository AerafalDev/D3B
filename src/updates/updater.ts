import { error as logError } from "@tauri-apps/plugin-log";
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

/** What closing the window must do: close, wait for an install under way, or install the downloaded update first. */
export type CloseAction = "close" | "wait" | "install";

interface UpdaterStore {
  state: UpdateState;
  /** Checks the channel and downloads a newer version in the background; does nothing while one is under way. */
  checkAndDownload: () => Promise<void>;
  /** Installs the downloaded version and starts it. */
  restart: () => Promise<void>;
  /** What a request to close the window must do now. */
  closeAction: () => CloseAction;
  /** Installs the downloaded version, if any, without restarting, then quits. */
  installOnClose: () => Promise<void>;
}

// The Update object holds a handle in the back end; it stays outside the store, which keeps plain data only. It is
// kept after a failed install, so that closing the app tries again.
let downloaded: Update | null = null;

function fail(context: string, error: unknown): string {
  const text = error instanceof Error ? error.message : String(error);
  void logError(`update: ${context}: ${text}`).catch(() => {});
  return text;
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
      set({ state: { status: "failed", error: fail("check or download", error) } });
    }
  },

  restart: async () => {
    const update = downloaded;
    if (update === null || get().state.status === "installing") {
      return;
    }
    set({ state: { status: "installing", version: update.version } });
    try {
      await update.install();
      downloaded = null;
      await relaunch();
    } catch (error) {
      set({ state: { status: "ready", version: update.version, notes: update.body } });
      fail("install", error);
    }
  },

  closeAction: () => {
    if (get().state.status === "installing") {
      return "wait";
    }
    return downloaded === null ? "close" : "install";
  },

  installOnClose: async () => {
    const update = downloaded;
    if (update !== null) {
      set({ state: { status: "installing", version: update.version } });
      try {
        await update.install({ restartAfterInstall: false });
        downloaded = null;
      } catch (error) {
        fail("install on close", error);
      }
    }
    await exit(0);
  },
}));
