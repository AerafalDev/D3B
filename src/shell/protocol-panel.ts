import { create } from "zustand";

import { useAppSettings } from "@/settings/app-settings.ts";
import { useLayout } from "./layout-store.ts";

interface ProtocolPanelStore {
  /**
   * Whether the player closed the panel by hand since it last opened: a character's tab becoming active then no longer
   * opens it, until a click on a character online (the owner, 2026-11-17).
   */
  dismissed: boolean;
}

/** What the Protocol panel's opening remembers for the session (`docs/shell.md`, "Settings"). */
export const useProtocolPanel = create<ProtocolPanelStore>(() => ({ dismissed: false }));

/**
 * Opens the Protocol panel at the player's explicit ask, a click on a character online, even after a close by hand;
 * nothing without Advanced tools, where the panel has no view.
 */
export function revealProtocolPanel(): void {
  useProtocolPanel.setState({ dismissed: false });
  if (useAppSettings.getState().advancedTools) {
    useLayout.getState().setVisible("panel", true);
  }
}
