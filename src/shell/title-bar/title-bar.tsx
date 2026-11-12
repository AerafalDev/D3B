import { useTranslation } from "react-i18next";
import { platform } from "@/lib/platform.ts";
import appIcon from "../../../src-tauri/icons/32x32.png";
import { CommandCentre } from "./command-centre.tsx";
import { LayoutToggles } from "./layout-toggles.tsx";
import { MenuBar } from "./menu-bar.tsx";
import { WindowControls } from "./window-controls.tsx";

/**
 * VS Code's title bar on Windows: the app's icon and menus, the command centre in the middle, the layout toggles and
 * the window's buttons. Its empty parts drag the window and double-clicked maximise it. On macOS and Linux the system
 * keeps its own frame and buttons above it.
 */
export function TitleBar() {
  const { t } = useTranslation();
  return (
    <header
      data-tauri-drag-region
      className="bg-frame grid h-[35px] shrink-0 grid-cols-[1fr_auto_1fr] items-center select-none"
    >
      <div data-tauri-drag-region className="flex h-full min-w-0 items-center gap-1 pl-2.5">
        <img src={appIcon} alt={t("app.name")} draggable={false} className="mr-1 size-4" />
        <MenuBar />
      </div>
      <CommandCentre />
      <div data-tauri-drag-region className="flex h-full items-center justify-end">
        <LayoutToggles />
        {platform() === "windows" && <WindowControls />}
      </div>
    </header>
  );
}
