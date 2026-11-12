import type { TFunction } from "i18next";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import type { GameConnectionStatus, RelayStatus } from "@/bindings.ts";
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

/** A tooltip's line, set in under the one before it when it details it. */
type Line = { text: string; detail?: boolean };

/**
 * A game connection on two lines: who it is (its build, its protocol, its map), then, when it is read, its counts; one
 * line when it is not read (`docs/protocol.md`, "In the app", the owner's choice).
 */
function connectionLines(connection: GameConnectionStatus, t: TFunction, format: (count: number) => string): Line[] {
  const { number, build, map } = connection;
  const protocol = t(`relay.protocol.${connection.protocol}`);
  const who =
    build === null
      ? t("relay.connection", { number, protocol })
      : map
        ? t("relay.connectionMap", { number, build, protocol, x: map.x, y: map.y })
        : t("relay.connectionBuild", { number, build, protocol });
  if (connection.protocol !== "on" && connection.protocol !== "lost") {
    return [{ text: who }];
  }
  const counts = t("relay.counts", {
    understood: format(connection.understood),
    read: format(connection.read),
    unknown: format(connection.unknown),
    notTrusted: format(connection.notTrusted),
    dropped: format(connection.dropped),
  });
  return [{ text: who }, { text: counts, detail: true }];
}

/** The relay in the status bar: its state and game connections, and in its tooltip what the player needs to know. */
export function RelayIndicator() {
  const { t, i18n } = useTranslation();
  const status = useRelay((store) => store.status);
  const follow = useRelay((store) => store.follow);

  useEffect(() => follow(), [follow]);

  if (status === null) {
    return null;
  }
  const label =
    status.state === "ready" ? t("relay.ready", { count: status.gameConnections }) : t(`relay.${status.state}`);
  const numbers = new Intl.NumberFormat(i18n.language);
  const plain = (text: string | null): Line[] => (text === null ? [] : [{ text }]);
  const details: Line[] = [
    ...plain(status.reason ?? null),
    ...plain(status.clientBuild ? t("relay.client", { build: status.clientBuild }) : null),
    ...plain(status.notUnderstood > 0 ? t("relay.notUnderstood", { count: status.notUnderstood }) : null),
    ...status.connections.flatMap((connection) => connectionLines(connection, t, (count) => numbers.format(count))),
    ...plain(status.gameConnections > 0 ? t("relay.quitEnds") : null),
    ...plain(status.state === "ready" ? t("relay.startAfter") : null),
  ];

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
        // The registry's tooltip lays its children in a row: the lines go one under the other.
        <TooltipContent className="max-w-md flex-col items-start gap-0.5">
          {details.map((line) => (
            <p key={line.text} className={cn(line.detail && "pl-3")}>
              {line.text}
            </p>
          ))}
        </TooltipContent>
      )}
    </Tooltip>
  );
}
