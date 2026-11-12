import { useTranslation } from "react-i18next";
import { platform } from "@/lib/platform.ts";
import appIcon from "../../../src-tauri/icons/32x32.png";
import { CommandCentre } from "./command-centre.tsx";
import { LayoutToggles } from "./layout-toggles.tsx";
import { MenuBar } from "./menu-bar.tsx";
import { WindowControls } from "./window-controls.tsx";

/**
 * VS Code's title bar on Windows: the app's icon and menus, the command centre in the middle, the layout toggles and
 * the window's buttons. All of it but its controls drags the window, and double-clicked maximises it. On macOS and
 * Linux the system keeps its own frame and buttons above it; on macOS the system's menu bar holds the menus, as VS
 * Code's does, so none is drawn.
 */
export function TitleBar() {
  const { t } = useTranslation();
  return (
    <header
      // "deep": a press anywhere but on a control (a button, a menu) drags; a bare attribute drags only on itself.
      data-tauri-drag-region="deep"
      className="bg-frame grid h-[35px] shrink-0 grid-cols-[1fr_auto_1fr] items-center select-none"
    >
      <div className="flex h-full min-w-0 items-center gap-1 pl-2.5">
        <img src={appIcon} alt={t("app.name")} draggable={false} className="mr-1 size-4" />
        {platform() !== "mac" && <MenuBar />}
      </div>
      <CommandCentre />
      <div className="flex h-full items-center justify-end">
        <LayoutToggles />
        {platform() === "windows" && <WindowControls />}
      </div>
    </header>
  );
}
