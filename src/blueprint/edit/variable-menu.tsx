import { useLayoutEffect, useRef } from "react";
import { useTranslation } from "react-i18next";

interface VariableMenuProps {
  /** Where it opens, in pixels from the graph's top left corner. */
  at: { x: number; y: number };
  /** The variable's name, as the player reads it. */
  name: string;
  onPick: (does: "get" | "set") => void;
  onClose: () => void;
}

/**
 * The small menu a variable dropped on the graph opens at the pointer (the owner's choice of 2026-11-17, Unreal's):
 * read it or write it. The arrows move between both, Enter chooses, Escape or a click elsewhere closes it.
 */
export function VariableMenu({ at, name, onPick, onClose }: VariableMenuProps) {
  const { t } = useTranslation();
  const menu = useRef<HTMLDivElement>(null);

  // Placed by its ref, as the picker is, kept inside the graph, its first item focused without scrolling the tab.
  useLayoutEffect(() => {
    const shown = menu.current;
    const room = shown?.parentElement;
    if (shown === null || room === null || room === undefined) {
      return;
    }
    const inside = (wanted: number, size: number, space: number) => Math.max(8, Math.min(wanted, space - size - 8));
    shown.style.left = `${inside(at.x, shown.offsetWidth, room.clientWidth)}px`;
    shown.style.top = `${inside(at.y, shown.offsetHeight, room.clientHeight)}px`;
    shown.querySelector("button")?.focus({ preventScroll: true });
  }, [at.x, at.y]);

  const item = (does: "get" | "set", label: string) => (
    <button
      type="button"
      role="menuitem"
      onClick={() => onPick(does)}
      className="hover:bg-hover focus-visible:bg-focus/30 rounded px-2 py-1 text-left whitespace-nowrap outline-none"
    >
      {label}
    </button>
  );

  return (
    <div
      ref={menu}
      role="menu"
      aria-label={t("blueprint.dropVariable", { name })}
      onKeyDown={(event) => {
        const items = [...(menu.current?.querySelectorAll("button") ?? [])];
        const now = items.indexOf(document.activeElement as HTMLButtonElement);
        if (event.key === "Escape") {
          event.preventDefault();
          onClose();
        } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
          event.preventDefault();
          items[(now + (event.key === "ArrowDown" ? 1 : items.length - 1)) % items.length]?.focus();
        }
      }}
      onBlur={(event) => {
        if (!menu.current?.contains(event.relatedTarget as Node | null)) {
          onClose();
        }
      }}
      className="absolute z-10 flex flex-col gap-px rounded-md border border-[#3c3c3c] bg-[#1f1f1f] p-1 text-xs text-white shadow-lg"
    >
      {item("get", t("blueprint.get", { name }))}
      {item("set", t("blueprint.set", { name }))}
    </div>
  );
}
