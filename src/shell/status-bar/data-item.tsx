import { useTranslation } from "react-i18next";

import type { AppInfo } from "@/bindings.ts";
import { Icon } from "@/icons/icon.tsx";
import { StatusItem } from "./status-item.tsx";

interface DataItemProps {
  info: AppInfo;
}

/**
 * The game data the app packs: the client build it comes from, or that there is none, with the reason in its hover
 * and JetBrains' yellow warning, one of the bar's two colours kept for a problem (the owner, 2026-11-13).
 */
export function DataItem({ info }: DataItemProps) {
  const { t } = useTranslation();
  if (info.gameBuild) {
    const client = t("status.client", { build: info.gameBuild });
    return (
      <StatusItem
        lead={<Icon name="library" className="size-3.5" />}
        label={client}
        hover={{ title: t("status.data"), sections: [[client]] }}
      />
    );
  }
  return (
    <StatusItem
      lead={<Icon name="warning" className="size-3.5" />}
      label={t("status.noData")}
      hover={{ title: t("status.noData"), sections: info.dataError ? [[info.dataError]] : [] }}
    />
  );
}
