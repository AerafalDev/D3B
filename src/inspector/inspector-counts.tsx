import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { useInspector } from "./inspector-store.ts";

const COUNTS = ["understood", "read", "unknown", "notTrusted", "dropped"] as const;

/**
 * The counts of the connection watched, in the panel's header: its frames understood, read, unknown and not trusted,
 * and those dropped, since it opened. A protocol not read has none to show. Frames not trusted are a problem, in colour.
 */
export function InspectorCounts() {
  const { t, i18n } = useTranslation();
  const watched = useInspector((store) => store.connection);
  const status = useRelay((store) => store.status?.connections.find((one) => one.number === watched));

  if (status === undefined || (status.protocol !== "on" && status.protocol !== "lost")) {
    return null;
  }
  const numbers = new Intl.NumberFormat(i18n.language);
  return (
    <div className="text-text-muted flex min-w-0 gap-3 overflow-hidden text-xs whitespace-nowrap">
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
  );
}
