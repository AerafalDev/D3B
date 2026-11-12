import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { IconButton } from "../icon-button.tsx";
import { useLayout } from "../layout-store.ts";
import { useShortcut } from "../use-shortcut.ts";

/** The title bar's three toggles, as VS Code's: the primary side bar, the panel, the secondary side bar. */
export function LayoutToggles() {
  const { t } = useTranslation();
  const visible = useLayout((store) => store.visible);
  const toggle = useLayout((store) => store.toggle);
  const shortcut = useShortcut();

  return (
    <div className="flex items-center gap-0.5 px-1">
      <IconButton
        label={t("shell.commands.primarySideBar")}
        shortcut={shortcut("primarySideBar")}
        pressed={visible.primarySideBar}
        onClick={() => toggle("primarySideBar")}
      >
        <Icon name="moveToLeft" />
      </IconButton>
      <IconButton
        label={t("shell.commands.panel")}
        shortcut={shortcut("panel")}
        pressed={visible.panel}
        onClick={() => toggle("panel")}
      >
        {/* JetBrains has no bottom-panel icon: the left one turned a quarter puts its strip at the bottom. */}
        <Icon name="moveToLeft" className="-rotate-90" />
      </IconButton>
      <IconButton
        label={t("shell.commands.secondarySideBar")}
        shortcut={shortcut("secondarySideBar")}
        pressed={visible.secondarySideBar}
        onClick={() => toggle("secondarySideBar")}
      >
        <Icon name="moveToRight" />
      </IconButton>
    </div>
  );
}
