import { useEffect } from "react";

import { characterOf } from "@/characters/characters-view.tsx";
import { useLayout } from "@/shell/layout-store.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { useInspector } from "./inspector-store.ts";

/** The connection the inspector shows: that of the character whose tab is active, while the panel shows; else none. */
export function useActiveConnection(): number | null {
  const shown = useLayout((store) => store.visible.panel);
  const active = useEditors((store) => store.active);
  return shown ? characterOf(active) : null;
}

/**
 * Watches the active connection, and none otherwise: the back end streams one connection's frames at a time, and only
 * while someone looks (`docs/protocol.md`, "The inspector"). The store's own connection is followed too, so that a
 * store reset while a tab stays active (the front end following anew) watches that tab again. Returns the connection
 * watched.
 */
export function useWatchedConnection(): number | null {
  const connection = useActiveConnection();
  const watched = useInspector((store) => store.connection);

  useEffect(() => {
    if (watched !== connection) {
      useInspector.getState().watch(connection);
    }
  }, [connection, watched]);
  useEffect(() => () => useInspector.getState().watch(null), []);

  return connection;
}
