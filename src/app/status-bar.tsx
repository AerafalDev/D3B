import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { type AppInfo, commands } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";
import { RelayIndicator } from "@/relay/relay-indicator.tsx";

/** The bottom bar: the app's version, the client build its data comes from, and the relay. */
export function StatusBar() {
  const { t } = useTranslation();
  const [info, setInfo] = useState<AppInfo | null>(null);

  useEffect(() => {
    if (!isTauri()) {
      return;
    }
    let live = true;
    commands.appInfo().then((value) => {
      if (live) {
        setInfo(value);
      }
    });
    return () => {
      live = false;
    };
  }, []);

  let data = t("status.browserPreview");
  if (info !== null) {
    data = info.gameBuild ? t("status.data", { build: info.gameBuild }) : t("status.noData");
  }

  return (
    <footer className="text-muted-foreground flex h-6 shrink-0 items-center gap-4 border-t px-3 text-xs">
      <span>D3B {info?.version ?? "dev"}</span>
      <span title={info?.dataError ?? undefined}>{data}</span>
      <RelayIndicator />
    </footer>
  );
}
