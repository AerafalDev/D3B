import { ChevronDown, ChevronRight } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { MyZones } from "@/zone/my-zones.tsx";
import { openWorld } from "./open-world.ts";
import { useWorld } from "./world-store.ts";
import { type TreeArea, worldTree } from "./world-tree.ts";

/**
 * The World view of the primary side bar (the owner's mock-up 10 of 2026-11-15): a search, then the game's sub-areas
 * grouped by area in a folded tree, which the search unfolds; a sub-area no world shows greyed, said "not on the map".
 * A sub-area chosen shows on the World tab. My zones come first (step 3's PR 20); my routes come with PR 21.
 */
export function WorldView() {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  const load = useWorld((store) => store.load);
  const picked = useWorld((store) => store.picked);
  const pick = useWorld((store) => store.pick);
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState<ReadonlySet<number>>(new Set());
  // A search unfolds what it finds; the areas folded during it, kept apart, so the tree before it comes back after.
  const [foldedInSearch, setFoldedInSearch] = useState<ReadonlySet<number>>(new Set());

  useEffect(() => {
    void load();
  }, [load]);

  const tree = read?.state === "read" ? worldTree(read.atlas, i18n.language, query) : [];
  const searching = query.trim() !== "";
  const toggle = (area: number) =>
    (searching ? setFoldedInSearch : setOpen)((now) => {
      const next = new Set(now);
      if (!next.delete(area)) {
        next.add(area);
      }
      return next;
    });

  const area = (one: TreeArea) => {
    const unfolded = searching ? !foldedInSearch.has(one.id) : open.has(one.id);
    return (
      <li key={one.id}>
        <button
          type="button"
          aria-expanded={unfolded}
          onClick={() => toggle(one.id)}
          className="hover:bg-hover focus-visible:outline-focus mx-1 flex h-6 w-[calc(100%-0.5rem)] items-center gap-1 rounded-[5px] px-1.5 text-left focus-visible:outline-1"
        >
          {unfolded ? (
            <ChevronDown aria-hidden="true" size={14} className="shrink-0" />
          ) : (
            <ChevronRight aria-hidden="true" size={14} className="shrink-0" />
          )}
          <span className="min-w-0 flex-1 truncate">{one.name}</span>
          <span className="text-text-faint text-[11px]">{one.subAreas.length}</span>
        </button>
        {unfolded && (
          <ul>
            {one.subAreas.map((subArea) => (
              <li key={subArea.id}>
                <button
                  type="button"
                  title={subArea.drawn ? undefined : t("world.notOnMap")}
                  aria-current={picked === subArea.id ? "true" : undefined}
                  onClick={() => {
                    pick(subArea.id);
                    openWorld();
                  }}
                  className="hover:bg-hover focus-visible:outline-focus mx-1 flex h-6 w-[calc(100%-0.5rem)] items-center gap-2 rounded-[5px] pr-2 pl-[26px] text-left focus-visible:outline-1 aria-[current=true]:bg-[#2a2c30] aria-[current=true]:text-white"
                >
                  <Icon name={subArea.drawn ? "mapPin" : "mapPinOff"} className="size-3.5" />
                  <span
                    className={subArea.drawn ? "min-w-0 flex-1 truncate" : "text-text-faint min-w-0 flex-1 truncate"}
                  >
                    {subArea.name}
                  </span>
                  {!subArea.drawn && <span className="sr-only">{t("world.notOnMap")}</span>}
                  <span className="text-text-faint text-[11px] whitespace-nowrap">
                    {t("world.level", { level: subArea.level })}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </li>
    );
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <label className="focus-within:border-focus mx-2 mb-1 flex h-[26px] items-center gap-1.5 rounded border border-[#3c3c3c] bg-[#181818] px-2 text-xs">
        <Icon name="search" className="size-3.5" />
        <input
          type="search"
          value={query}
          onChange={(event) => {
            setQuery(event.target.value);
            setFoldedInSearch(new Set());
          }}
          placeholder={t("world.search")}
          aria-label={t("world.search")}
          className="placeholder:text-text-muted min-w-0 flex-1 bg-transparent outline-0"
        />
      </label>
      <div className="min-h-0 flex-1 overflow-y-auto pb-2 text-[13px]">
        <MyZones query={query} />
        <h3 className="text-text-muted mt-1 flex h-[22px] items-center px-3 text-[11px] font-semibold tracking-[.04em] uppercase">
          {t("world.subAreas")}
        </h3>
        {read === null || read.state === "loading" ? (
          <p className="text-text-muted px-3 text-xs">{t("world.loading")}</p>
        ) : read.state === "failed" ? (
          <p className="text-text-muted px-3 text-xs">{t("world.noData", { reason: read.error.reason })}</p>
        ) : tree.length === 0 ? (
          <p className="text-text-muted px-3 text-xs">{t(searching ? "world.nothingFound" : "world.noWorld")}</p>
        ) : (
          <ul>{tree.map(area)}</ul>
        )}
      </div>
    </div>
  );
}
