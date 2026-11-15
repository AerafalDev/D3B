import { ChevronDown, Crosshair, MapPin, MapPinOff } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { named } from "@/characters/names.ts";
import { Command, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command.tsx";
import { Popover, PopoverAnchor, PopoverContent, PopoverTrigger } from "@/components/ui/popover.tsx";
import { openWorld } from "@/world/open-world.ts";
import { findSubArea, useWorld } from "@/world/world-store.ts";
import { worldTree } from "@/world/world-tree.ts";

/** A Zone's value as a blueprint's file holds it (`bot::variable`): a game's sub-area, or a zone of mine. */
export type ZoneValue = { sub_area: number } | { zone: string };

interface ZoneFieldProps {
  /** The zone chosen, `null` while none is. */
  value: ZoneValue | null;
  onChange: (value: ZoneValue) => void;
  /** Whose Zone it is, as the map's banner says while choosing on it. */
  who: string;
}

/**
 * A Zone setting (the owner's mock-up 12, 2026-11-15): the zone chosen with its area and level; a click unfolds a list
 * under it, searched as the World view, my zones first (none until they are painted), then the game's sub-areas by
 * area and level, the arrows and Enter choosing; its target, or the list's last line, chooses on the World tab's map.
 */
export function ZoneField({ value, onChange, who }: ZoneFieldProps) {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  const load = useWorld((store) => store.load);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const atlas = read?.state === "read" ? read.atlas : null;
  const tree = useMemo(
    () => (atlas === null ? [] : worldTree(atlas, i18n.language, query)),
    [atlas, i18n.language, query],
  );

  useEffect(() => {
    void load();
  }, [load]);

  const choose = (chosen: ZoneValue) => {
    setOpen(false);
    setQuery("");
    onChange(chosen);
  };
  const chooseOnMap = () => {
    setOpen(false);
    useWorld.getState().startChoosing(who, (subArea) => onChange({ sub_area: subArea }));
    openWorld();
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverAnchor asChild>
        <div className="focus-within:border-focus flex h-[30px] items-center gap-1 rounded border border-[#3c3c3c] bg-[#181818] pr-1 text-xs">
          <PopoverTrigger asChild>
            <button
              type="button"
              role="combobox"
              aria-label={t("zone.label")}
              aria-expanded={open}
              className="flex h-full min-w-0 flex-1 items-center gap-1.5 pl-2 text-left outline-0"
            >
              <MapPin aria-hidden="true" size={14} strokeWidth={1.5} className="shrink-0 text-[#39c5bb]" />
              <ZoneText value={value} />
              <ChevronDown
                aria-hidden="true"
                size={14}
                strokeWidth={1.5}
                className="text-text-muted ml-auto shrink-0"
              />
            </button>
          </PopoverTrigger>
          <button
            type="button"
            aria-label={t("zone.onMap")}
            title={t("zone.onMap")}
            onClick={chooseOnMap}
            className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-6 shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1"
          >
            <Crosshair aria-hidden="true" size={14} strokeWidth={1.5} />
          </button>
        </div>
      </PopoverAnchor>
      <PopoverContent align="start" className="w-(--radix-popper-anchor-width) p-1">
        <Command shouldFilter={false} label={t("zone.search")}>
          <CommandInput placeholder={t("zone.search")} value={query} onValueChange={setQuery} />
          <CommandList className="max-h-[300px]">
            <CommandGroup heading={t("zone.mine")}>
              <p className="text-text-faint px-2 pb-1 text-xs">{t("zone.mineLater")}</p>
            </CommandGroup>
            {tree.length === 0 && query.trim() !== "" && (
              <p className="text-text-muted px-2 py-1 text-xs">{t("world.nothingFound")}</p>
            )}
            {tree.map((area) => (
              <CommandGroup key={area.id} heading={area.name}>
                {area.subAreas.map((subArea) => (
                  <CommandItem
                    key={subArea.id}
                    value={String(subArea.id)}
                    onSelect={() => choose({ sub_area: subArea.id })}
                    className="h-6 text-xs data-selected:bg-[#04395e]"
                  >
                    {subArea.drawn ? (
                      <MapPin aria-hidden="true" size={14} strokeWidth={1.5} className="text-[#39c5bb]" />
                    ) : (
                      <MapPinOff aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-faint" />
                    )}
                    {subArea.name}
                    <span className="text-text-faint ml-auto text-[11px]">
                      {t("world.level", { level: subArea.level })}
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}
            <CommandGroup>
              <CommandItem value="map" onSelect={chooseOnMap} className="h-6 text-xs data-selected:bg-[#04395e]">
                <Crosshair aria-hidden="true" size={14} strokeWidth={1.5} />
                {t("zone.onMap")}
              </CommandItem>
            </CommandGroup>
          </CommandList>
          <p className="text-text-faint border-line border-t px-2 pt-1 text-right text-[11px]">{t("zone.keys")}</p>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

/** The zone chosen as the field says it: a sub-area's name, its area and level; a zone of mine's id until mine come. */
function ZoneText({ value }: { value: ZoneValue | null }) {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  if (value === null) {
    return <span className="text-text-faint truncate">{t("zone.toChoose")}</span>;
  }
  if ("zone" in value) {
    return <span className="truncate text-white">{value.zone}</span>;
  }
  if (read?.state !== "read") {
    return null;
  }
  const subArea = findSubArea(read.atlas, value.sub_area);
  if (subArea === undefined) {
    return <span className="truncate text-[#f2c55c]">{t("zone.missing", { id: value.sub_area })}</span>;
  }
  return (
    <>
      <span className="truncate text-white">{named(subArea.name, i18n.language)}</span>
      <span className="text-text-faint truncate text-[11px]">
        {[named(subArea.area.name, i18n.language), t("world.level", { level: subArea.level })].join(" · ")}
      </span>
    </>
  );
}
