import { useTranslation } from "react-i18next";

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable.tsx";
import { cn } from "@/lib/utils.ts";
import { CommandPalette } from "./command-palette.tsx";
import { useLayout } from "./layout-store.ts";
import { ActivityBar } from "./parts/activity-bar.tsx";
import { EditorArea } from "./parts/editor-area.tsx";
import { PartFrame } from "./parts/part-frame.tsx";
import { ResizablePart } from "./parts/resizable-part.tsx";
import { StatusBar } from "./parts/status-bar.tsx";
import { SIDE_VIEWS } from "./side-views.ts";
import { TitleBar } from "./title-bar/title-bar.tsx";
import { useShellShortcuts } from "./use-shell-shortcuts.ts";

// The 5 px gaps between the islands, turning blue once held under the pointer, as VS Code's sashes.
const SASH =
  "bg-transparent w-[5px] aria-[orientation=horizontal]:h-[5px] hover:bg-focus data-[separator=active]:bg-focus transition-colors delay-150";

/**
 * The app's shell in VS Code's layout (the owner, 2026-11-12): the activity bar, the primary side bar, the editor area
 * over the panel, and the secondary side bar; the status bar; the title bar above them all. Each part is a discreet
 * island on the window's frame, 5 px apart (the owner's "2 / 3 sober", then a little more room).
 */
export function Shell() {
  useShellShortcuts();
  const { t } = useTranslation();
  const visible = useLayout((store) => store.visible);
  const shownView = useLayout((store) => store.view);
  const active = SIDE_VIEWS.find((entry) => entry.id === shownView);
  const ActiveView = active?.component;

  return (
    <div className="bg-frame text-text flex h-svh flex-col overflow-hidden">
      <TitleBar />
      <div className="flex min-h-0 flex-1 pr-[5px]">
        <ActivityBar />
        <ResizablePanelGroup orientation="horizontal" className="min-w-0 flex-1">
          <ResizablePart part="primarySideBar" defaultSize={300} minSize={170}>
            <PartFrame part="primarySideBar" title={active === undefined ? undefined : t(active.title)}>
              {ActiveView === undefined ? undefined : <ActiveView />}
            </PartFrame>
          </ResizablePart>
          <ResizableHandle className={cn(SASH, !visible.primarySideBar && "hidden")} />
          <ResizablePanel id="centre" minSize={300} className="overflow-hidden!">
            <ResizablePanelGroup orientation="vertical">
              <ResizablePanel id="editor" minSize={120} className="overflow-hidden!">
                <EditorArea />
              </ResizablePanel>
              <ResizableHandle className={cn(SASH, !visible.panel && "hidden")} />
              <ResizablePart part="panel" defaultSize={260} minSize={100}>
                <PartFrame part="panel" />
              </ResizablePart>
            </ResizablePanelGroup>
          </ResizablePanel>
          <ResizableHandle className={cn(SASH, !visible.secondarySideBar && "hidden")} />
          <ResizablePart part="secondarySideBar" defaultSize={300} minSize={170}>
            <PartFrame part="secondarySideBar" />
          </ResizablePart>
        </ResizablePanelGroup>
      </div>
      <StatusBar />
      <CommandPalette />
    </div>
  );
}
