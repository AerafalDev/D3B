import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip.tsx";
import { cn } from "@/lib/utils.ts";

interface IconButtonProps {
  /** What it does, its accessible name and its hover. */
  label: string;
  /** Its shortcut as the platform writes it, shown after the label in the hover. */
  shortcut?: string;
  /** For a toggle: whether what it toggles is on. */
  pressed?: boolean;
  onClick: () => void;
  className?: string;
  children: ReactNode;
}

/** A bar's icon button, as VS Code's: a 22 px square with a rounded hover, its label in a hover after a short delay. */
export function IconButton({ label, shortcut, pressed, onClick, className, children }: IconButtonProps) {
  const { t } = useTranslation();
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type="button"
          aria-label={label}
          aria-pressed={pressed}
          onClick={onClick}
          className={cn(
            "text-text hover:bg-hover aria-pressed:bg-pressed focus-visible:outline-focus flex size-[22px] items-center justify-center rounded-[5px] focus-visible:outline-1 focus-visible:-outline-offset-1",
            className,
          )}
        >
          {children}
        </button>
      </TooltipTrigger>
      <TooltipContent
        side="bottom"
        sideOffset={4}
        className="text-text border border-[#454545] bg-[#202020] px-2 py-1 text-xs shadow-md [&>span:last-child]:hidden"
      >
        {shortcut === undefined ? label : t("shell.withShortcut", { label, shortcut })}
      </TooltipContent>
    </Tooltip>
  );
}
