import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils.ts";
import { frameTime } from "./frame-time.ts";
import type { FrameSummary } from "./inspector-store.ts";

/** The list's columns: number, time, side, message, outcome, size; the header and each row share them. */
export const COLUMNS = "grid grid-cols-[64px_84px_18px_minmax(0,1fr)_84px_72px] items-center gap-x-1";

interface FrameRowProps {
  frame: FrameSummary;
  /** Whether it is the frame chosen, whose detail shows. */
  selected: boolean;
  onSelect: (number: number) => void;
}

/**
 * A frame in the list: its number, its time since the connection opened, its side, its message by its readable name
 * (or by the build's name, dimmer, when none is pinned), its outcome, and its size. Only a frame not trusted is in
 * colour: it is a problem. Chosen, it is a rounded grey row, as a list's selection is in the shell.
 */
export function FrameRow({ frame, selected, onSelect }: FrameRowProps) {
  const { t, i18n } = useTranslation();
  const side = frame.fromClient ? "client" : "server";
  const numbers = new Intl.NumberFormat(i18n.language);

  return (
    <button
      type="button"
      aria-current={selected ? "true" : undefined}
      onClick={() => onSelect(frame.number)}
      className={cn(
        COLUMNS,
        "hover:bg-hover focus-visible:outline-focus mx-1 h-[22px] w-[calc(100%-0.5rem)] rounded-[4px] px-2 text-left text-xs whitespace-nowrap tabular-nums focus-visible:outline-1 focus-visible:-outline-offset-1",
        selected && "bg-[#2a2c30] hover:bg-[#2a2c30]",
      )}
    >
      <span className="text-text-muted text-right">{frame.number}</span>
      <span className="text-text-muted pl-2">{frameTime(frame.millis)}</span>
      <span className="text-text-muted">
        <span aria-hidden="true">{frame.fromClient ? "↑" : "↓"}</span>
        <span className="sr-only">{t(`inspector.side.${side}`)}</span>
      </span>
      {frame.readable === null ? (
        <span className="text-text-faint truncate font-mono text-[11.5px]">{frame.name ?? "—"}</span>
      ) : (
        <span className="truncate">{frame.readable}</span>
      )}
      <span className={cn("text-text-muted", frame.outcome === "notTrusted" && "text-error")}>
        {t(`inspector.outcome.${frame.outcome}`)}
      </span>
      <span className="text-text-muted text-right">{t("inspector.size", { size: numbers.format(frame.size) })}</span>
    </button>
  );
}
