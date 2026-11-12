import { useTranslation } from "react-i18next";

import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip.tsx";
import { Icon } from "@/icons/icon.tsx";
import { useLayout } from "../layout-store.ts";
import { SIDE_VIEWS } from "../side-views.ts";

/**
 * VS Code's activity bar, 48 px wide, one entry per view of the primary side bar. The view showing is a rounded pill,
 * not VS Code's blue line (the owner's "2 / 3 sober"); choosing it again hides the side bar.
 */
export function ActivityBar() {
  const { t } = useTranslation();
  const view = useLayout((store) => store.view);
  const shown = useLayout((store) => store.visible.primarySideBar);
  const showView = useLayout((store) => store.showView);

  return (
    <nav aria-label={t("shell.parts.activityBar")} className="bg-frame flex w-12 shrink-0 flex-col items-center gap-1">
      {SIDE_VIEWS.map((entry) => (
        <Tooltip key={entry.id}>
          <TooltipTrigger asChild>
            <button
              type="button"
              aria-label={t(entry.title)}
              aria-pressed={shown && view === entry.id}
              onClick={() => showView(entry.id)}
              className="hover:bg-hover focus-visible:outline-focus flex size-9 items-center justify-center rounded-[7px] opacity-70 hover:opacity-100 focus-visible:outline-1 aria-pressed:bg-[#262626] aria-pressed:opacity-100"
            >
              <Icon name={entry.icon} className="size-5" />
            </button>
          </TooltipTrigger>
          <TooltipContent
            side="right"
            sideOffset={6}
            className="text-text border border-[#454545] bg-[#202020] px-2 py-1 text-xs shadow-md [&>span:last-child]:hidden"
          >
            {t(entry.title)}
          </TooltipContent>
        </Tooltip>
      ))}
    </nav>
  );
}
