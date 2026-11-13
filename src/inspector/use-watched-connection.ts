import { useEffect } from "react";

import { characterOf } from "@/characters/characters-view.tsx";
import { useLayout } from "@/shell/layout-store.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { useInspector } from "./inspector-store.ts";

/**
 * Watches the connection of the character whose tab is active while the panel shows, and none otherwise: the back end
 * streams one connection's frames at a time, and only while someone looks (`docs/protocol.md`, "The inspector").
 * Returns the connection watched.
 */
export function useWatchedConnection(): number | null {
  const shown = useLayout((store) => store.visible.panel);
  const active = useEditors((store) => store.active);
  const connection = shown ? characterOf(active) : null;

  useEffect(() => {
    useInspector.getState().watch(connection);
  }, [connection]);
  useEffect(() => () => useInspector.getState().watch(null), []);

  return connection;
}
