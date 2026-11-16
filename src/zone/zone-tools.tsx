import { Brush, Eraser, Grid2x2Plus, MousePointer2 } from "lucide-react";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { WORLD_EDITOR } from "@/world/open-world.ts";
import { type Tool, useWorld } from "@/world/world-store.ts";

/** The tools in order, each with its picture and key (the owner, 2026-11-16). */
const TOOLS = [
  { tool: "select", Glyph: MousePointer2, key: "v" },
  { tool: "brush", Glyph: Brush, key: "b" },
  { tool: "eraser", Glyph: Eraser, key: "e" },
  { tool: "subArea", Glyph: Grid2x2Plus, key: "s" },
] as const satisfies readonly { tool: Tool; Glyph: unknown; key: string }[];

/**
 * The World tab's toolbar while a zone of mine is open (the owner's choices of 2026-11-16), floating on top of the map:
 * Select, Brush, Eraser and Whole sub-area, each also by its key, V, B, E and S, and Space held to move the map with
 * the main button, wherever the focus is while the World tab is the active one, unless a field takes the keys.
 */
export function ZoneTools() {
  const { t } = useTranslation();
  const tool = useWorld((store) => store.tool);
  const take = useWorld((store) => store.takeTool);

  useEffect(() => {
    // Keys for the map only: not those a field types, nor those of another tab while the World's is behind it.
    const ours = (event: KeyboardEvent) => {
      const target = event.target;
      const typing = target instanceof Element && target.closest("input, textarea, select, [contenteditable]") !== null;
      return (
        !typing &&
        !event.defaultPrevented &&
        !event.ctrlKey &&
        !event.metaKey &&
        !event.altKey &&
        useEditors.getState().active === WORLD_EDITOR
      );
    };
    const down = (event: KeyboardEvent) => {
      if (!ours(event)) {
        return;
      }
      const chosen = TOOLS.find((one) => one.key === event.key.toLowerCase());
      if (event.key === " ") {
        // Not a press of the button or row that holds the focus.
        useWorld.setState({ panning: true });
      } else if (chosen !== undefined) {
        take(chosen.tool);
      } else {
        return;
      }
      event.preventDefault();
    };
    const up = (event: KeyboardEvent) => {
      if (event.key === " " && useWorld.getState().panning) {
        useWorld.setState({ panning: false });
        event.preventDefault();
      }
    };
    const lost = () => useWorld.setState({ panning: false });
    window.addEventListener("keydown", down);
    window.addEventListener("keyup", up);
    window.addEventListener("blur", lost);
    return () => {
      window.removeEventListener("keydown", down);
      window.removeEventListener("keyup", up);
      window.removeEventListener("blur", lost);
      lost();
    };
  }, [take]);

  return (
    <div
      role="toolbar"
      aria-label={t("zone.tools.label")}
      className="absolute top-3 left-1/2 flex -translate-x-1/2 gap-0.5 rounded-lg border border-[#3c3c3c] bg-[#1b1c1f] p-0.5 shadow-[0_6px_20px_rgb(0_0_0/45%)]"
    >
      {TOOLS.map(({ tool: one, Glyph, key }) => {
        const label = t(`zone.tools.${one}`, { key: key.toUpperCase() });
        return (
          <button
            key={one}
            type="button"
            aria-pressed={tool === one}
            aria-label={label}
            title={label}
            onClick={() => take(one)}
            className={cn(
              "hover:bg-hover focus-visible:outline-focus flex size-7 items-center justify-center rounded-md focus-visible:outline-1",
              tool === one ? "bg-[#04395e] text-white" : "text-text-muted",
            )}
          >
            <Glyph aria-hidden="true" size={15} strokeWidth={1.5} />
          </button>
        );
      })}
    </div>
  );
}
