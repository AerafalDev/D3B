import { convertFileSrc } from "@tauri-apps/api/core";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { commands, type ItemView_Serialize } from "@/bindings.ts";
import { named } from "@/characters/names.ts";

interface ItemFieldProps {
  /** The item chosen, its id as JSON; `null` while none is. */
  value: string | null;
  /** The item chosen, its id as JSON, saved at once; whether it was kept. */
  onChange: (value: string) => Promise<boolean>;
  label: string;
}

/** How long a search waits for the next key before asking the back end. */
const PAUSE = 150;

function chosenId(json: string | null): number | null {
  if (json === null) {
    return null;
  }
  try {
    const value: unknown = JSON.parse(json);
    return typeof value === "number" ? value : null;
  } catch {
    return null;
  }
}

/**
 * An item (the owner, 2026-11-17): the one chosen shown by its icon, name and level, and a search by name among every
 * item but the invisible ones (Rust's `find_items`), each found with its icon, level and type; a click, or Enter for
 * the first, chooses it, Escape clears the search.
 */
export function ItemField({ value, onChange, label }: ItemFieldProps) {
  const { t, i18n } = useTranslation();
  const id = chosenId(value);
  const [chosen, setChosen] = useState<ItemView_Serialize | null>(null);
  useEffect(() => {
    if (id === null) {
      setChosen(null);
      return;
    }
    let live = true;
    commands
      .findItems("", [id])
      .then((found) => live && setChosen(found[0] ?? null))
      // Without the game data, the item is said by its id.
      .catch(() => live && setChosen(null));
    return () => {
      live = false;
    };
  }, [id]);
  const [query, setQuery] = useState("");
  // What the search found for the text typed, `null` while it is asked: Enter never takes an earlier search's.
  const [found, setFound] = useState<ItemView_Serialize[] | null>([]);
  useEffect(() => {
    const wanted = query.trim();
    if (wanted === "") {
      setFound([]);
      return;
    }
    setFound(null);
    let live = true;
    const timer = setTimeout(() => {
      commands
        .findItems(wanted, [])
        .then((items) => live && setFound(items))
        .catch(() => live && setFound([]));
    }, PAUSE);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [query]);
  const pick = (item: ItemView_Serialize) => {
    const before = chosen;
    setQuery("");
    setFound([]);
    setChosen(item);
    // Refused, the item saved before shows again.
    void onChange(String(item.id)).then((kept) => kept || setChosen(before));
  };
  const results = found ?? [];
  const name = (item: ItemView_Serialize) => named(item.name, i18n.language) ?? String(item.id);
  return (
    <div className="flex flex-col gap-1 text-xs">
      {id !== null && (
        <span className="flex h-[22px] items-center gap-1.5 text-white">
          {chosen?.icon != null && <img src={convertFileSrc(chosen.icon)} alt="" className="size-4" />}
          {chosen === null
            ? t("library.value.item", { id })
            : t("values.itemLevel", { name: name(chosen), level: chosen.level })}
        </span>
      )}
      <input
        type="search"
        aria-label={t("values.searchItem", { label })}
        placeholder={t("values.searchItemHint")}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        onKeyDown={(event) => {
          const first = results[0];
          if (event.key === "Enter" && first !== undefined) {
            pick(first);
          } else if (event.key === "Escape") {
            setQuery("");
          }
        }}
        className="focus:border-focus h-[26px] min-w-0 rounded border border-[#3c3c3c] bg-[#181818] px-2 text-white outline-0"
      />
      <span aria-live="polite" className="text-text-muted">
        {query.trim() !== "" && found !== null && t("values.found", { count: found.length })}
      </span>
      {results.length > 0 && (
        <ul aria-label={t("values.itemsFound")} className="flex max-h-48 flex-col overflow-y-auto">
          {results.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => pick(item)}
                className="hover:bg-hover focus-visible:outline-focus flex w-full items-center gap-1.5 rounded px-1 py-0.5 text-left focus-visible:outline-1"
              >
                {item.icon !== null && <img src={convertFileSrc(item.icon)} alt="" className="size-4" />}
                <span className="truncate text-white">{name(item)}</span>
                <span className="text-text-muted ml-auto whitespace-nowrap">
                  {t("values.itemKind", { kind: named(item.kind, i18n.language) ?? "", level: item.level })}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
