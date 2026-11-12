import { DockviewReact } from "dockview-react";

import { CharacterTab } from "@/characters/character-tab.tsx";
import { useEditors } from "./editor-store.ts";
import { EditorTab } from "./editor-tab.tsx";
import { EditorWatermark } from "./editor-watermark.tsx";

// Dockview's dark theme with Dark Modern's colours (src/index.css, `.d3b-dockview`).
const THEME = { name: "d3b", className: "dockview-theme-dark d3b-dockview", colorScheme: "dark" } as const;

// The editors a tab can hold, by the name `openEditor` gives.
const EDITORS = { character: CharacterTab };

/**
 * VS Code's editor area, an island: tabs and splits, by dockview. Each tab is drawn by `EditorTab`; with no editor
 * open, it shows its watermark.
 */
export function EditorArea() {
  const setApi = useEditors((store) => store.setApi);
  return (
    <div className="border-line h-full overflow-hidden rounded-[6px] border">
      <DockviewReact
        className="bg-editor h-full"
        theme={THEME}
        components={EDITORS}
        defaultTabComponent={EditorTab}
        watermarkComponent={EditorWatermark}
        onReady={(event) => setApi(event.api)}
      />
    </div>
  );
}
