import { useTranslation } from "react-i18next";

import type { Gauge as GaugeValue } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";

/** Each gauge's colour (`src/index.css`, `.d3b-gauge-*`). */
export type GaugeTone = "life" | "pods" | "experience" | "energy";

interface GaugeProps {
  label: string;
  gauge: GaugeValue | null;
  tone: GaugeTone;
  /** A note after the label, such as a life still regenerating. */
  note?: string;
}

/** A labelled gauge: its value over its maximum, and a thin bar drawn by a `<meter>`. */
export function Gauge({ label, gauge, tone, note }: GaugeProps) {
  const { i18n } = useTranslation();
  const numbers = new Intl.NumberFormat(i18n.language);
  const known = gauge !== null && gauge.value !== null && gauge.max !== null && gauge.max > 0;
  return (
    <div className="flex flex-col gap-1">
      <div className="flex justify-between gap-2 text-xs">
        <span className="text-text-muted">
          {label}
          {note !== undefined && <span className="text-text-faint"> · {note}</span>}
        </span>
        <span className="tabular-nums">
          {known ? `${numbers.format(gauge.value ?? 0)} / ${numbers.format(gauge.max ?? 0)}` : "—"}
        </span>
      </div>
      <meter
        aria-label={label}
        min={0}
        max={known ? (gauge.max ?? 1) : 1}
        value={known ? (gauge.value ?? 0) : 0}
        className={cn("d3b-gauge h-1 w-full", `d3b-gauge-${tone}`)}
      />
    </div>
  );
}
