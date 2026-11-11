import { getCurrentWindow } from "@tauri-apps/api/window";
import { useEffect } from "react";

import { isTauri } from "@/lib/tauri.ts";
import { useUpdater } from "./updater.ts";

/**
 * Checks for an update once the app starts, downloads it in the background, and installs it when the window closes if
 * the player did not restart for it. Off in development and in the browser preview.
 */
export function useAutoUpdate(): void {
  useEffect(() => {
    if (!isTauri() || import.meta.env.DEV) {
      return;
    }
    const { checkAndDownload, installOnClose } = useUpdater.getState();
    void checkAndDownload();

    const closing = getCurrentWindow().onCloseRequested(async (event) => {
      if (useUpdater.getState().state.status === "ready") {
        event.preventDefault();
        await installOnClose();
      }
    });
    return () => {
      void closing.then((unlisten) => unlisten());
    };
  }, []);
}
