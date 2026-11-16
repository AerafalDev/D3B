import { Plus } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuTrigger } from "@/components/ui/context-menu.tsx";
import { Icon } from "@/icons/icon.tsx";
import { openWorld } from "@/world/open-world.ts";
import { useWorld } from "@/world/world-store.ts";
import { folded } from "@/world/world-tree.ts";
import { type MyZone, useZones } from "./zones-store.ts";

interface MyZonesProps {
  /** The World view's search, which my zones' names are matched against too. */
  query: string;
}

/**
 * "My zones" at the top of the World view (the owner's choices of 2026-11-16): "+" makes "Nouvelle zone" on the world
 * the tab shows and opens it there with the brush; a row opens its zone, F2 or a right-click renames it in place, and
 * the right-click deletes it to the system's trash.
 */
export function MyZones({ query }: MyZonesProps) {
  const { t } = useTranslation();
  const zones = useZones((store) => store.zones);
  const refresh = useZones((store) => store.refresh);
  const open = useWorld((store) => store.zone);
  const [renaming, setRenaming] = useState<string | null>(null);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const create = async () => {
    const world = useWorld.getState().world;
    const id = await useZones.getState().create(world);
    if (id !== null) {
      useWorld.getState().openZone(id, world, "brush");
      openWorld();
    }
  };
  const show = (zone: MyZone) => {
    useWorld.getState().openZone(zone.id, zone.world);
    openWorld();
  };
  const remove = async (zone: MyZone) => {
    if ((await useZones.getState().remove(zone.id)) && useWorld.getState().zone === zone.id) {
      useWorld.setState({ zone: null });
    }
  };
  const sought = folded(query.trim());
  const listed = zones
    .filter((zone) => sought === "" || folded(zone.name).includes(sought))
    .sort((a, b) => a.name.localeCompare(b.name));

  return (
    <section>
      <h3 className="text-text-muted mt-1 flex h-[22px] items-center pr-1 pl-3 text-[11px] font-semibold tracking-[.04em] uppercase">
        <span className="flex-1">{t("zone.mine")}</span>
        <button
          type="button"
          aria-label={t("zone.create")}
          title={t("zone.create")}
          onClick={() => void create()}
          className="hover:bg-hover focus-visible:outline-focus flex size-5 items-center justify-center rounded hover:text-white focus-visible:outline-1"
        >
          <Plus aria-hidden="true" size={14} strokeWidth={1.5} />
        </button>
      </h3>
      {zones.length === 0 ? (
        <p className="text-text-faint px-3 pb-1 text-xs">{t("zone.none")}</p>
      ) : (
        <ul>
          {listed.map((zone) =>
            renaming === zone.id ? (
              <li key={zone.id}>
                <Renaming zone={zone} done={() => setRenaming(null)} />
              </li>
            ) : (
              <li key={zone.id}>
                <ContextMenu>
                  <ContextMenuTrigger asChild>
                    <button
                      type="button"
                      aria-current={open === zone.id ? "true" : undefined}
                      onClick={() => show(zone)}
                      onKeyDown={(event) => {
                        if (event.key === "F2") {
                          event.preventDefault();
                          setRenaming(zone.id);
                        }
                      }}
                      className="hover:bg-hover focus-visible:outline-focus mx-1 flex h-6 w-[calc(100%-0.5rem)] items-center gap-2 rounded-[5px] pr-2 pl-3 text-left focus-visible:outline-1 aria-[current=true]:bg-[#2a2c30] aria-[current=true]:text-white"
                    >
                      <Icon name="mapPin" className="size-3.5 text-[#e2a33a]" />
                      <span className="min-w-0 flex-1 truncate">{zone.name}</span>
                      <span className="text-text-faint text-[11px] whitespace-nowrap">
                        {t("world.maps", { count: zone.maps.length })}
                      </span>
                    </button>
                  </ContextMenuTrigger>
                  <ContextMenuContent>
                    <ContextMenuItem onSelect={() => setRenaming(zone.id)}>{t("zone.rename")}</ContextMenuItem>
                    <ContextMenuItem onSelect={() => void remove(zone)}>{t("zone.delete")}</ContextMenuItem>
                  </ContextMenuContent>
                </ContextMenu>
              </li>
            ),
          )}
        </ul>
      )}
    </section>
  );
}

/** A zone's name edited in its row: Enter or leaving saves it, Escape leaves it as it was. */
function Renaming({ zone, done }: { zone: MyZone; done: () => void }) {
  const { t } = useTranslation();
  const [name, setName] = useState(zone.name);
  const save = () => {
    done();
    if (name.trim() !== "" && name !== zone.name) {
      void useZones.getState().rename(zone.id, name);
    }
  };
  return (
    <input
      // biome-ignore lint/a11y/noAutofocus: F2 or the menu asked to type the name now
      autoFocus
      aria-label={t("zone.name")}
      value={name}
      onChange={(event) => setName(event.target.value)}
      onFocus={(event) => event.currentTarget.select()}
      onBlur={save}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.currentTarget.blur();
        } else if (event.key === "Escape") {
          event.preventDefault();
          setName(zone.name);
          done();
        }
      }}
      className="border-focus mx-1 h-6 w-[calc(100%-0.5rem)] rounded-[5px] border bg-[#181818] px-2 text-[13px] outline-0"
    />
  );
}
