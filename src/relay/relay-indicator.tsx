import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import type { RelayStatus } from "@/bindings.ts";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip.tsx";
import { cn } from "@/lib/utils.ts";
import { useRelay } from "./relay-store.ts";

const TONE: Record<RelayStatus["state"], string> = {
  starting: "bg-muted-foreground",
  ready: "bg-emerald-500",
  failed: "bg-destructive",
  unsupported: "bg-muted-foreground",
  stopped: "bg-muted-foreground",
};

/** The relay in the status bar: its state and game connections, and in its tooltip what the player needs to know. */
export function RelayIndicator() {
  const { t } = useTranslation();
  const status = useRelay((store) => store.status);
  const follow = useRelay((store) => store.follow);

  useEffect(() => follow(), [follow]);

  if (status === null) {
    return null;
  }
  const label =
    status.state === "ready" ? t("relay.ready", { count: status.gameConnections }) : t(`relay.${status.state}`);
  const details = [
    status.reason ?? null,
    status.clientBuild ? t("relay.client", { build: status.clientBuild }) : null,
    status.notUnderstood > 0 ? t("relay.notUnderstood", { count: status.notUnderstood }) : null,
    status.gameConnections > 0 ? t("relay.quitEnds") : null,
    status.state === "ready" ? t("relay.startAfter") : null,
  ].filter((line) => line !== null);

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        {/* Focusable, so that the tooltip opens from the keyboard too. */}
        {/* biome-ignore lint/a11y/noNoninteractiveTabindex: a tooltip's trigger must take the focus */}
        <span tabIndex={0} className="flex items-center gap-1.5 outline-none focus-visible:underline">
          <span aria-hidden className={cn("size-2 rounded-full", TONE[status.state])} />
          {label}
        </span>
      </TooltipTrigger>
      {details.length > 0 && (
        <TooltipContent>
          {details.map((line) => (
            <p key={line}>{line}</p>
          ))}
        </TooltipContent>
      )}
    </Tooltip>
  );
}
