import type { Gauge } from "@/bindings.ts";
import { cn } from "@/lib/utils.ts";

const TONE = {
  life: "[&::-moz-progress-bar]:bg-emerald-500 [&::-webkit-progress-value]:bg-emerald-500",
  pods: "[&::-moz-progress-bar]:bg-sky-500 [&::-webkit-progress-value]:bg-sky-500",
  experience: "[&::-moz-progress-bar]:bg-violet-500 [&::-webkit-progress-value]:bg-violet-500",
  energy: "[&::-moz-progress-bar]:bg-amber-500 [&::-webkit-progress-value]:bg-amber-500",
} as const;

/** A bar filled as `gauge` is, labelled for screen readers; a gauge without a positive max shows empty. */
export function GaugeBar({
  gauge,
  tone,
  label,
  thin,
}: {
  gauge: Gauge;
  tone: keyof typeof TONE;
  label: string;
  thin?: boolean;
}) {
  const max = gauge.max ?? 0;
  return (
    <progress
      aria-label={label}
      value={max > 0 ? Math.min(max, Math.max(0, gauge.value ?? 0)) : 0}
      max={max > 0 ? max : 1}
      className={cn(
        "[&::-webkit-progress-bar]:bg-muted bg-muted block w-full appearance-none overflow-hidden rounded-full [&::-webkit-progress-value]:rounded-full",
        thin ? "h-1" : "h-2",
        TONE[tone],
      )}
    />
  );
}
