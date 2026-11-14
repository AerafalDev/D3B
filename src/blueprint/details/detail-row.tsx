import type { ReactNode } from "react";

interface DetailRowProps {
  label: string;
  children: ReactNode;
}

/** A line of Details, the prototype's: its label above, what it says under it. */
export function DetailRow({ label, children }: DetailRowProps) {
  return (
    <div className="flex flex-col gap-1">
      <span className="text-text-muted">{label}</span>
      {children}
    </div>
  );
}
