import { getCurrentWindow } from "@tauri-apps/api/window";
import { useTranslation } from "react-i18next";

import {
  Menubar,
  MenubarCheckboxItem,
  MenubarContent,
  MenubarItem,
  MenubarMenu,
  MenubarSeparator,
  MenubarShortcut,
  MenubarTrigger,
} from "@/components/ui/menubar.tsx";
import { isTauri } from "@/lib/tauri.ts";
import { useAppSettings } from "@/settings/app-settings.ts";
import { type Part, useLayout } from "../layout-store.ts";
import { useShortcut } from "../use-shortcut.ts";

// VS Code's menus: 26 px rows, the row under the pointer in the accent blue.
const TRIGGER = "text-text h-[22px] rounded-[4px] px-2 text-[13px] font-normal hover:bg-hover aria-expanded:bg-hover";
const CONTENT = "min-w-60 rounded-lg border border-[#454545] bg-[#1f1f1f] p-1 shadow-xl ring-0";
const ITEM = "text-text h-[26px] rounded-[4px] pr-2 pl-7 text-[13px] focus:bg-focus focus:text-white";
const SHORTCUT = "text-text-muted pl-8 tracking-normal group-focus/menubar-item:text-white";

const PARTS: readonly Part[] = ["primarySideBar", "panel", "secondarySideBar"];

/** The title bar's menus. Each holds only what works today; the features add theirs. */
export function MenuBar() {
  const { t } = useTranslation();
  const visible = useLayout((store) => store.visible);
  const toggle = useLayout((store) => store.toggle);
  const openPalette = useLayout((store) => store.setPaletteOpen);
  const shortcut = useShortcut();
  const advancedTools = useAppSettings((store) => store.advancedTools);
  const setAdvancedTools = useAppSettings((store) => store.setAdvancedTools);
  // The panel's one view is the Protocol view until step 4's History: without Advanced tools, it is not offered.
  const parts = advancedTools ? PARTS : PARTS.filter((part) => part !== "panel");

  return (
    <Menubar className="h-auto gap-0 border-none bg-transparent p-0 shadow-none">
      <MenubarMenu>
        <MenubarTrigger className={TRIGGER}>{t("shell.menu.file")}</MenubarTrigger>
        <MenubarContent className={CONTENT}>
          <MenubarItem className={ITEM} onSelect={() => isTauri() && void getCurrentWindow().close()}>
            {t("shell.menu.exit")}
          </MenubarItem>
        </MenubarContent>
      </MenubarMenu>
      <MenubarMenu>
        <MenubarTrigger className={TRIGGER}>{t("shell.menu.view")}</MenubarTrigger>
        <MenubarContent className={CONTENT}>
          <MenubarItem className={ITEM} onSelect={() => openPalette(true)}>
            {t("shell.menu.commandPalette")}
            <MenubarShortcut className={SHORTCUT}>{shortcut("palette")}</MenubarShortcut>
          </MenubarItem>
          <MenubarSeparator className="mx-0 my-1 bg-[#454545]" />
          {parts.map((part) => (
            <MenubarCheckboxItem
              key={part}
              className={ITEM}
              checked={visible[part]}
              onCheckedChange={() => toggle(part)}
            >
              {t(`shell.parts.${part}`)}
              <MenubarShortcut className={SHORTCUT}>{shortcut(part)}</MenubarShortcut>
            </MenubarCheckboxItem>
          ))}
          <MenubarSeparator className="mx-0 my-1 bg-[#454545]" />
          <MenubarCheckboxItem
            className={ITEM}
            checked={advancedTools}
            onCheckedChange={() => setAdvancedTools(!advancedTools)}
          >
            {t("shell.menu.advancedTools")}
          </MenubarCheckboxItem>
        </MenubarContent>
      </MenubarMenu>
    </Menubar>
  );
}
