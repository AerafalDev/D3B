import i18next from "i18next";
import { Search } from "lucide-react";
import { useLayoutEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import type { ChoiceView_Serialize } from "@/bindings.ts";
import { nodeWord } from "@/i18n/node-words.ts";
import { cn } from "@/lib/utils.ts";
import { FAMILY_COLOUR, nodeIcon } from "../graph-look.ts";
import { pinLabel } from "../pin-label.ts";
import type { LinkEnd } from "./link-check.ts";
import { pickerChoices } from "./picker-choices.ts";

interface PickerProps {
  /** Where it opens, in pixels from the graph's top left corner. */
  at: { x: number; y: number };
  choices: readonly ChoiceView_Serialize[];
  /** The pin a wire dropped on the ground came from, if it did. */
  dragged: LinkEnd | null;
  /** That pin's label as its node draws it, "Alors 2"; its kind's label when not given. */
  draggedLabel?: string;
  /** The blueprint's variables' and functions' names by id, as the player reads them. */
  names: ReadonlyMap<string, string>;
  onPick: (choice: ChoiceView_Serialize) => void;
  onClose: () => void;
}

/** A choice's sentence, its holes left open, and its description, in the player's language. */
function said(choice: ChoiceView_Serialize, names: ReadonlyMap<string, string>): [string, string] {
  const t = i18next.t;
  switch (choice.does) {
    case "get":
      return [names.get(choice.variable) ?? choice.variable, t("blueprint.picker.get")];
    case "set":
      return [t("blueprint.set", { name: names.get(choice.variable) ?? choice.variable }), t("blueprint.picker.set")];
    case "call":
      return [names.get(choice.function) ?? choice.function, t("blueprint.picker.call")];
    case "kind": {
      const sentence = nodeWord(`kinds.${choice.kind}.sentence`);
      return [
        sentence === null ? choice.kind : sentence.replace(/\{\{\w+\}\}/g, "…"),
        nodeWord(`kinds.${choice.kind}.description`) ?? "",
      ];
    }
  }
}

/**
 * The picker (`docs/bot.md`, "In the app"; the prototype's card, the owner's choices of 2026-11-16): a search, then
 * "Often here" and every choice by family, each its sentence and description, an action the bot does not do yet tagged
 * with its step; with a wire dragged, only what its pin fits. The arrows and Enter choose, Escape closes.
 */
export function Picker({ at, choices, dragged, draggedLabel, names, onPick, onClose }: PickerProps) {
  const { t } = useTranslation();
  const card = useRef<HTMLDivElement>(null);
  const field = useRef<HTMLInputElement>(null);
  const [search, setSearch] = useState("");
  const [active, setActive] = useState(0);
  const words = (choice: ChoiceView_Serialize) => said(choice, names).join(" ");
  const { often, all } = pickerChoices(choices, dragged, search, words);
  const flat = [...often, ...all.flatMap(([, inFamily]) => inFamily)];

  // The row chosen by the arrows stays in sight.
  // biome-ignore lint/correctness/useExhaustiveDependencies: the row to show is the one `active` names
  useLayoutEffect(() => {
    card.current?.querySelector("[aria-selected='true']")?.scrollIntoView?.({ block: "nearest" });
  }, [active]);

  // Placed by its ref, as the World's labels are, kept inside the graph, and focused without scrolling the tab to it.
  useLayoutEffect(() => {
    const shown = card.current;
    const room = shown?.parentElement;
    if (shown === null || room === null || room === undefined) {
      return;
    }
    const inside = (wanted: number, size: number, space: number) => Math.max(8, Math.min(wanted, space - size - 8));
    shown.style.left = `${inside(at.x, shown.offsetWidth, room.clientWidth)}px`;
    shown.style.top = `${inside(at.y, shown.offsetHeight, room.clientHeight)}px`;
    field.current?.focus({ preventScroll: true });
  }, [at.x, at.y]);

  const row = (choice: ChoiceView_Serialize, index: number) => {
    const [sentence, description] = said(choice, names);
    const Icon = nodeIcon(choice.family, choice.does === "kind" ? choice.kind : null);
    return (
      <button
        type="button"
        key={`${index}-${sentence}`}
        id={`picker-${index}`}
        role="option"
        aria-selected={index === active}
        onClick={() => onPick(choice)}
        onMouseEnter={() => setActive(index)}
        onFocus={() => setActive(index)}
        className={cn("flex w-full items-start gap-2 rounded px-2 py-1 text-left", index === active && "bg-[#2a2c30]")}
      >
        <Icon
          aria-hidden="true"
          size={14}
          strokeWidth={1.5}
          color={FAMILY_COLOUR[choice.family ?? "flow"]}
          className="mt-0.5 shrink-0"
        />
        <span className="flex min-w-0 flex-1 flex-col">
          <span className="text-text">{sentence}</span>
          {description !== "" && <span className="text-text-faint text-[11px]">{description}</span>}
        </span>
        {choice.fromStep !== null && (
          <span className="text-text-faint text-[11px]">{t("blueprint.fromStep", { step: choice.fromStep })}</span>
        )}
      </button>
    );
  };

  let index = 0;
  return (
    <div
      ref={card}
      role="dialog"
      aria-label={t("blueprint.picker.title")}
      className="bg-bar border-line absolute z-10 flex max-h-[min(380px,calc(100%-16px))] w-[340px] flex-col rounded-md border text-xs shadow-lg"
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          onClose();
        } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
          event.preventDefault();
          const step = event.key === "ArrowDown" ? 1 : -1;
          setActive((now) => (flat.length === 0 ? 0 : (now + step + flat.length) % flat.length));
        } else if (event.key === "Enter" && event.target === field.current) {
          const choice = flat[active];
          if (choice !== undefined) {
            onPick(choice);
          }
        }
      }}
    >
      <label className="border-line flex items-center gap-2 border-b px-2.5 py-2">
        <Search aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-muted" />
        <input
          ref={field}
          role="combobox"
          aria-expanded="true"
          aria-controls="picker-list"
          aria-activedescendant={flat.length === 0 ? undefined : `picker-${active}`}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setActive(0);
          }}
          placeholder={t("blueprint.picker.search")}
          className="text-text placeholder:text-text-faint min-w-0 flex-1 bg-transparent outline-none"
        />
        {dragged !== null && (
          <span className="text-text-faint shrink-0 text-[11px]">
            {t("blueprint.picker.after", { pin: draggedLabel ?? pinLabel(dragged.pin.id) })}
          </span>
        )}
      </label>
      <div id="picker-list" role="listbox" className="flex flex-col gap-0.5 overflow-y-auto p-1">
        {flat.length === 0 && <p className="text-text-muted px-2 py-1">{t("blueprint.picker.none")}</p>}
        {often.length > 0 && (
          <>
            <p className="text-text-faint px-2 pt-1 text-[11px]">{t("blueprint.picker.often")}</p>
            {often.map((choice) => row(choice, index++))}
          </>
        )}
        {all.map(([family, inFamily]) => (
          <div key={family} className="flex flex-col gap-0.5">
            <p className="text-text-faint px-2 pt-1 text-[11px]">{t(`blueprint.picker.family.${family}`)}</p>
            {inFamily.map((choice) => row(choice, index++))}
          </div>
        ))}
      </div>
    </div>
  );
}
