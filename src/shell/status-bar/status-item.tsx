import type { ReactNode } from "react";

import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip.tsx";
import { cn } from "@/lib/utils.ts";

/**
 * A status item's hover, as VS Code's rich hovers: sections of lines set apart by a rule, the first one brighter. No
 * title: a hover says only what the bar does not (the owner, 2026-11-13).
 */
export type StatusHover = string[][];

interface StatusItemProps {
  /** A 14 px icon or the app's dot, before the label. */
  lead?: ReactNode;
  label: string;
  hover?: StatusHover;
  className?: string;
}

const ITEM = "flex h-[22px] items-center gap-[5px] rounded-[4px] px-1.5";

/**
 * A status bar item, as VS Code's: an icon and a short label on the frame. With a hover it takes the focus and a
 * rounded grey hover, so that its details open from the keyboard too.
 */
export function StatusItem({ lead, label, hover, className }: StatusItemProps) {
  const content = (
    <>
      {lead}
      <span className="truncate">{label}</span>
    </>
  );
  if (hover === undefined || hover.length === 0) {
    return <span className={cn(ITEM, className)}>{content}</span>;
  }
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          // biome-ignore lint/a11y/noNoninteractiveTabindex: a hover's trigger must take the focus
          tabIndex={0}
          className={cn(ITEM, "hover:bg-hover focus-visible:outline-focus focus-visible:outline-1", className)}
        >
          {content}
        </span>
      </TooltipTrigger>
      <TooltipContent
        side="top"
        sideOffset={4}
        className="text-text max-w-sm flex-col items-stretch gap-0 border border-[#454545] bg-[#202020] px-2.5 py-2 text-xs leading-relaxed shadow-md [&>span:last-child]:hidden"
      >
        {hover.map((lines, index) => (
          // A section's lines are its identity.
          <div
            key={lines.join("\n")}
            className={cn(index > 0 && "text-text-muted mt-1.5 border-t border-[#353535] pt-1.5")}
          >
            {lines.map((line) => (
              <p key={line}>{line}</p>
            ))}
          </div>
        ))}
      </TooltipContent>
    </Tooltip>
  );
}
