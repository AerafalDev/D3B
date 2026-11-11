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
    const updater = useUpdater.getState();
    void updater.checkAndDownload();

    // A JS close listener takes the close over: the window is destroyed unless the handler prevents it, which needs
    // `core:window:allow-destroy` in the capabilities.
    const closing = getCurrentWindow().onCloseRequested(async (event) => {
      const action = useUpdater.getState().closeAction();
      if (action === "close") {
        return;
      }
      event.preventDefault();
      if (action === "install") {
        await useUpdater.getState().installOnClose();
      }
    });
    return () => {
      void closing.then((unlisten) => unlisten());
    };
  }, []);
}
