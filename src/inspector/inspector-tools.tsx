import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { cn } from "@/lib/utils.ts";
import { IconButton } from "@/shell/icon-button.tsx";
import { type SideShown, useInspector } from "./inspector-store.ts";

const SIDES: readonly SideShown[] = ["all", "client", "server"];
const ARROWS = { all: "", client: "↑ ", server: "↓ " } as const;

/**
 * The list's tools, on the right of the panel's header: which side's frames show, the filter by name, and the pause,
 * pressed while the list stands still, with how many frames arrived meanwhile.
 */
export function InspectorTools() {
  const { t } = useTranslation();
  const side = useInspector((store) => store.side);
  const name = useInspector((store) => store.name);
  const paused = useInspector((store) => store.paused);
  const held = useInspector((store) => store.held.length);
  const { setSide, setName, pause } = useInspector.getState();

  return (
    <div className="flex shrink-0 items-center gap-2 text-xs">
      <fieldset
        aria-label={t("inspector.sides")}
        className="flex overflow-hidden rounded-[4px] border border-[#2b2b2b]"
      >
        {SIDES.map((one) => (
          <button
            key={one}
            type="button"
            aria-pressed={side === one}
            onClick={() => setSide(one)}
            className={cn(
              "text-text-muted hover:bg-hover focus-visible:outline-focus px-2 py-px whitespace-nowrap focus-visible:outline-1 focus-visible:-outline-offset-1",
              side === one && "text-text bg-[#2a2c30] hover:bg-[#2a2c30]",
            )}
          >
            {ARROWS[one]}
            {one === "all" ? t("inspector.all") : t(`inspector.side.${one}`)}
          </button>
        ))}
      </fieldset>
      <label className="focus-within:border-focus flex h-[22px] w-[clamp(80px,15vw,190px)] items-center gap-1.5 rounded-[4px] border border-[#2b2b2b] bg-[#141414] px-1.5">
        <Icon name="search" className="size-3.5" />
        <input
          type="search"
          value={name}
          onChange={(event) => setName(event.target.value)}
          placeholder={t("inspector.filter")}
          aria-label={t("inspector.filter")}
          className="text-text placeholder:text-text-faint min-w-0 flex-1 bg-transparent outline-none"
        />
      </label>
      {paused && <span className="text-text-muted whitespace-nowrap">{t("inspector.held", { count: held })}</span>}
      <IconButton label={t("inspector.pause")} pressed={paused} onClick={() => pause(!paused)}>
        <Icon name="pause" />
      </IconButton>
    </div>
  );
}
