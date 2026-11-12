import { getCurrentWindow } from "@tauri-apps/api/window";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import type { IconName } from "@/icons/icons.ts";
import { isTauri } from "@/lib/tauri.ts";
import { cn } from "@/lib/utils.ts";

interface ControlProps {
  label: string;
  icon: IconName;
  onClick: () => void;
  danger?: boolean;
}

function Control({ label, icon, onClick, danger }: ControlProps) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={onClick}
      className={cn(
        "flex h-full w-[46px] items-center justify-center focus-visible:outline-none",
        danger ? "hover:bg-[#e81123] focus-visible:bg-[#e81123]" : "hover:bg-hover focus-visible:bg-hover",
      )}
    >
      <Icon name={icon} />
    </button>
  );
}

/**
 * Minimise, maximise or restore, and close, drawn as Windows draws them since the window has no frame there. Closing
 * goes through the window's close request, so an update still installs on close.
 */
export function WindowControls() {
  const { t } = useTranslation();
  const [maximized, setMaximized] = useState(false);

  useEffect(() => {
    if (!isTauri()) {
      return;
    }
    const window = getCurrentWindow();
    let live = true;
    const read = () =>
      void window.isMaximized().then((value) => {
        if (live) {
          setMaximized(value);
        }
      });
    read();
    const unlisten = window.onResized(read);
    return () => {
      live = false;
      void unlisten.then((stop) => stop());
    };
  }, []);

  const act = (action: "minimize" | "toggleMaximize" | "close") => {
    if (isTauri()) {
      void getCurrentWindow()[action]();
    }
  };

  return (
    <div className="flex h-full">
      <Control label={t("shell.window.minimize")} icon="windowMinimize" onClick={() => act("minimize")} />
      <Control
        label={maximized ? t("shell.window.restore") : t("shell.window.maximize")}
        icon={maximized ? "windowRestore" : "windowMaximize"}
        onClick={() => act("toggleMaximize")}
      />
      <Control label={t("shell.window.close")} icon="windowClose" onClick={() => act("close")} danger />
    </div>
  );
}
