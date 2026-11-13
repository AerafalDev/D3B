import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { InspectorTools } from "./inspector-tools.tsx";
import { useActiveConnection } from "./use-watched-connection.ts";

const COUNTS = ["understood", "read", "unknown", "notTrusted", "dropped"] as const;

/**
 * What the Protocol view adds to the panel's header, on its one line (the owner's layout of 2026-11-13): the active
 * connection's counts of frames understood, read, unknown and not trusted, and those dropped, since it opened; then on
 * the right the side, the name filter and the pause. A protocol not read has none of them. Frames not trusted are a
 * problem, in colour.
 */
export function InspectorHeader() {
  const { t, i18n } = useTranslation();
  const watched = useActiveConnection();
  const status = useRelay((store) => store.status?.connections.find((one) => one.number === watched));

  if (status === undefined || (status.protocol !== "on" && status.protocol !== "lost")) {
    return null;
  }
  const numbers = new Intl.NumberFormat(i18n.language);
  return (
    <>
      <div className="text-text-muted ml-1 flex min-w-0 gap-3 overflow-hidden text-xs whitespace-nowrap">
        {COUNTS.map((count) => {
          const problem = count === "notTrusted" && status[count] > 0;
          return (
            <span key={count} className={cn(problem && "text-error")}>
              {t(`inspector.counts.${count}`)}{" "}
              <b className={cn("text-text font-medium tabular-nums", problem && "text-error")}>
                {numbers.format(status[count])}
              </b>
            </span>
          );
        })}
      </div>
      <span className="min-w-3 flex-1" />
      <InspectorTools />
    </>
  );
}
