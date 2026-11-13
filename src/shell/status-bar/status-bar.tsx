import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import { RelayItem } from "@/relay/relay-item.tsx";
import { UpdateItem } from "@/updates/update-item.tsx";
import { useAppInfo } from "./app-info.ts";
import { DataItem } from "./data-item.tsx";
import { StatusItem } from "./status-item.tsx";

/**
 * VS Code's status bar, 22 px high, on the window's frame (the owner's "2 / 3 sober", 2026-11-12). As in VS Code, the
 * app's state on the left, its context on the right (the owner, 2026-11-13): the app's name and version after its one
 * accent, a small blue dot, then the relay; on the right the game data's build, then the offer to restart into a
 * downloaded update.
 */
export function StatusBar() {
  const { t } = useTranslation();
  const info = useAppInfo((store) => store.info);

  useEffect(() => {
    void useAppInfo.getState().load();
  }, []);

  return (
    <footer className="bg-frame text-text-muted flex h-[22px] shrink-0 items-center justify-between gap-2 px-1 text-xs">
      <div className="flex min-w-0 items-center gap-0.5">
        <StatusItem
          lead={<span aria-hidden="true" className="bg-focus size-1.5 shrink-0 rounded-full" />}
          label={info === null ? t("app.name") : t("status.version", { version: info.version })}
        />
        <RelayItem dataBuild={info?.gameBuild ?? null} />
      </div>
      <div className="flex min-w-0 items-center gap-0.5">
        {info !== null && <DataItem info={info} />}
        <UpdateItem />
      </div>
    </footer>
  );
}
