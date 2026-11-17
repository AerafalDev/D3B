import { useHotkeys } from "react-hotkeys-hook";

import { openWorld } from "@/world/open-world.ts";
import { useWorld } from "@/world/world-store.ts";
import { useZones } from "./zones-store.ts";

/**
 * Undoes my zones' last change, or redoes the last undone, and shows the zone it changed on the World tab's map, on its
 * world; a zone no longer there is closed (the owner's choices of 2026-11-17).
 */
export async function stepZones(back: boolean): Promise<void> {
  const stepped = await useZones.getState().step(back);
  if (stepped === null) {
    return;
  }
  const zone = stepped.zone ?? null;
  if (zone !== null) {
    useWorld.getState().openZone(stepped.id, zone.world);
    openWorld();
  } else if (useWorld.getState().zone === stepped.id) {
    useWorld.setState({ zone: null });
  }
}

/**
 * Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z (⌘ on macOS) undo and redo my zones' changes while the focus is in the element the
 * returned ref is given, the World tab or the World view; a field being typed in keeps its own.
 */
export function useZoneUndo<T extends HTMLElement>() {
  return useHotkeys<T>(
    "mod+z, mod+y, mod+shift+z",
    (_event, handler) => void stepZones(handler.hotkey === "mod+z"),
    // By the letter typed, not the key's place: Ctrl+Z is on another key in AZERTY and QWERTZ.
    { preventDefault: true, useKey: true },
  );
}
