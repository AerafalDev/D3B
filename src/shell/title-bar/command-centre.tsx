import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { useLayout } from "../layout-store.ts";
import { useShortcut } from "../use-shortcut.ts";

/** VS Code's command centre: the box in the middle of the title bar that opens the command palette. */
export function CommandCentre() {
  const { t } = useTranslation();
  const open = useLayout((store) => store.setPaletteOpen);
  const shortcut = useShortcut();
  const label = t("shell.withShortcut", {
    label: t("shell.commands.palette"),
    shortcut: shortcut("palette"),
  });

  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={() => open(true)}
      className="text-text-muted hover:text-text focus-visible:outline-focus flex h-[24px] w-[min(38vw,600px)] items-center justify-center gap-1.5 rounded-[6px] border border-white/[0.09] bg-white/[0.03] text-xs hover:border-white/20 hover:bg-white/[0.07] focus-visible:outline-1"
    >
      <Icon name="search" className="size-3.5 opacity-80" />
      <span>{t("app.name")}</span>
    </button>
  );
}
