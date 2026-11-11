import { Download } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button.tsx";
import { useUpdater } from "./updater.ts";

/** The bar under the title that offers to restart into a downloaded update. Hidden otherwise. */
export function UpdateBanner() {
  const { t } = useTranslation();
  const state = useUpdater((store) => store.state);
  const restart = useUpdater((store) => store.restart);

  if (state.status !== "ready" && state.status !== "installing") {
    return null;
  }

  return (
    <div
      role="status"
      className="bg-primary text-primary-foreground flex h-9 shrink-0 items-center gap-3 px-3 text-sm"
      title={state.status === "ready" ? state.notes : undefined}
    >
      <Download className="size-4" />
      <span className="flex-1">{t("updates.ready", { version: state.version })}</span>
      <Button size="sm" variant="secondary" disabled={state.status === "installing"} onClick={() => void restart()}>
        {state.status === "installing" ? t("updates.installing") : t("updates.restart")}
      </Button>
    </div>
  );
}
