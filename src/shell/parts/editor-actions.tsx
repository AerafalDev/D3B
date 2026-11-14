import type { IDockviewHeaderActionsProps } from "dockview-react";
import type { ReactNode } from "react";

import { BlueprintActions } from "@/library/blueprint-actions.tsx";

// The actions each editor has, by the name `openEditor` gives its component; step 4 adds a character's Play, Pause and
// Stop.
const ACTIONS: Record<string, (props: { tab: string }) => ReactNode> = { blueprint: BlueprintActions };

/**
 * VS Code's editor actions, at the right of a group's tabs: those of the editor its active tab holds (the owner,
 * 2026-11-14), none for an editor that has none.
 */
export function EditorActions({ activePanel }: IDockviewHeaderActionsProps) {
  const Actions = activePanel === undefined ? undefined : ACTIONS[activePanel.view.contentComponent];
  if (activePanel === undefined || Actions === undefined) {
    return null;
  }
  return (
    <div className="flex h-full items-center gap-0.5 pr-2">
      <Actions tab={activePanel.id} />
    </div>
  );
}
