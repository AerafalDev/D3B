import { create } from "zustand";

import { commands, events, type RelayStatus } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";

interface RelayStore {
  /** The relay's status, once the back end told it; `null` before, and in the browser preview. */
  status: RelayStatus | null;
  /** Follows the relay's status from the back end; returns what stops following. */
  follow: () => () => void;
}

export const useRelay = create<RelayStore>((set) => ({
  status: null,

  follow: () => {
    if (!isTauri()) {
      return () => {};
    }
    let live = true;
    // An event says the latest status; the first answer only fills in until one comes.
    let heard = false;
    const unlisten = events.relayChanged.listen((event) => {
      if (live) {
        heard = true;
        set({ status: event.payload });
      }
    });
    void commands.relayStatus().then((status) => {
      if (live && !heard) {
        set({ status });
      }
    });
    return () => {
      live = false;
      void unlisten.then((stop) => stop());
    };
  },
}));
