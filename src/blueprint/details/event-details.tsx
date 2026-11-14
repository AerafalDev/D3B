import { useTranslation } from "react-i18next";

import type { EventMode, EventView } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";
import { DetailRow } from "./detail-row.tsx";

const MODES: readonly EventMode[] = ["interruptThenResume", "waitForAction", "restart", "parallel"];

/**
 * A mode as a small timeline, the owner's choice of 2026-11-13 (screen 6, variant A): the main flow grey, the event
 * red, where it cuts in dashed.
 */
function Timeline({ mode }: { mode: EventMode }) {
  if (mode === "parallel") {
    return (
      <svg viewBox="0 0 220 20" className="h-[18px] w-[200px]" aria-hidden="true">
        <rect x="0" y="2" width="220" height="7" rx="2" fill="#5a5a5a" />
        <rect x="70" y="12" width="50" height="7" rx="2" fill="#f48771" />
      </svg>
    );
  }
  const cut = mode === "waitForAction" ? 40 : 70;
  return (
    <svg viewBox="0 0 220 20" className="h-[18px] w-[200px]" aria-hidden="true">
      <rect x="0" y="7" width="70" height="7" rx="2" fill="#5a5a5a" />
      <line x1={cut} y1="2" x2={cut} y2="19" stroke="#d6d6d6" strokeDasharray="2 2" />
      <rect x="72" y="7" width="60" height="7" rx="2" fill="#f48771" />
      <rect x="134" y="7" width="86" height="7" rx="2" fill="#5a5a5a" />
      {mode === "restart" && <rect x="134" y="3" width="2" height="15" fill="#d6d6d6" />}
    </svg>
  );
}

interface EventDetailsProps {
  /** Its settings, or `null` for the main flow, which takes none. */
  event: EventView | null;
  /** Its threshold and re-arm, when it has them, as the player reads them. */
  rearm: string | null;
}

/**
 * An event's Details (the owner's screen 6, variant A): its priority, its re-arming, and its four modes each drawn as
 * a small timeline, the one it has marked. Read-only until step 3's PR 16.
 */
export function EventDetails({ event, rearm }: EventDetailsProps) {
  const { t } = useTranslation();
  if (event === null) {
    return <DetailRow label={t("blueprint.details.event")}>{t("blueprint.details.mainFlow")}</DetailRow>;
  }
  return (
    <>
      <DetailRow label={t("blueprint.details.priority")}>
        <span>
          {event.priority} <span className="text-text-faint">· {t("blueprint.details.priorityHint")}</span>
        </span>
      </DetailRow>
      {rearm !== null && <DetailRow label={t("blueprint.details.rearm")}>{rearm}</DetailRow>}
      <DetailRow label={t("blueprint.details.when")}>
        <ul className="flex flex-col gap-1">
          {MODES.map((mode) => (
            <li
              key={mode}
              aria-current={mode === event.mode ? "true" : undefined}
              className={cn(
                "grid grid-cols-[14px_1fr] gap-x-2 gap-y-[3px] rounded-[6px] border border-transparent px-2 py-[6px]",
                mode === event.mode && "border-[#3c3c3c] bg-[#232427] text-white",
              )}
            >
              <span
                aria-hidden="true"
                className={cn(
                  "mt-0.5 size-3 rounded-full border border-[#5a5a5a]",
                  mode === event.mode && "border-focus border-4",
                )}
              />
              <span>{t(`blueprint.details.mode.${mode}`)}</span>
              <span className="col-start-2">
                <Timeline mode={mode} />
              </span>
            </li>
          ))}
        </ul>
      </DetailRow>
    </>
  );
}
