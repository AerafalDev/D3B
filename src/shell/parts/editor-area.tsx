import { DockviewReact } from "dockview-react";

import { EditorWatermark } from "./editor-watermark.tsx";

// Dockview's dark theme with Dark Modern's colours (src/index.css, `.d3b-dockview`).
const THEME = { name: "d3b", className: "dockview-theme-dark d3b-dockview", colorScheme: "dark" } as const;

/** VS Code's editor area, an island: tabs and splits, by dockview. No editor exists yet, so it shows its watermark. */
export function EditorArea() {
  return (
    <div className="border-line h-full overflow-hidden rounded-[6px] border">
      <DockviewReact
        className="bg-editor h-full"
        theme={THEME}
        components={{}}
        watermarkComponent={EditorWatermark}
        onReady={() => {}}
      />
    </div>
  );
}
