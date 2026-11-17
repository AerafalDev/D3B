import { useEffect } from "react";

import { characterOf } from "@/characters/characters-view.tsx";
import { useRelay } from "@/relay/relay-store.ts";
import { useAppSettings } from "@/settings/app-settings.ts";
import { useLayout } from "@/shell/layout-store.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { useProtocolPanel } from "@/shell/protocol-panel.ts";

/** The connection of the active tab when it shows a character whose client is connected; else `null`. */
function useConnectedTab(): number | null {
  const connection = characterOf(useEditors((store) => store.active));
  const connected = useRelay((store) => store.status?.connections.some((open) => open.number === connection) ?? false);
  return connected ? connection : null;
}

/**
 * Opens the Protocol panel, with Advanced tools, as a connected character's tab becomes active, the panel starting
 * closed (the owner, 2026-11-17, `docs/shell.md`); a close by hand holds while tabs change, until a click on a
 * character online (`revealProtocolPanel`). Advanced tools turning off is no close by hand.
 */
export function usePanelOpening(): void {
  const advancedTools = useAppSettings((store) => store.advancedTools);
  const connection = useConnectedTab();

  useEffect(
    () =>
      useLayout.subscribe((layout, before) => {
        if (layout.visible.panel !== before.visible.panel) {
          const byHand = !layout.visible.panel && useAppSettings.getState().advancedTools;
          useProtocolPanel.setState({ dismissed: byHand });
        }
      }),
    [],
  );

  useEffect(() => {
    if (!advancedTools) {
      useProtocolPanel.setState({ dismissed: false });
    } else if (connection !== null && !useProtocolPanel.getState().dismissed) {
      useLayout.getState().setVisible("panel", true);
    }
  }, [advancedTools, connection]);
}
