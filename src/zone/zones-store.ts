import i18next from "i18next";
import { toast } from "sonner";
import { create } from "zustand";

import { commands, type KeptFile, type ZoneError, type ZoneView } from "@/bindings.ts";

/** A zone of mine (`docs/bot.md`, "Storage"): its maps' coordinates on its one world. */
export type MyZone = ZoneView;

interface ZonesStore {
  /** My zones, in the order of their files; empty before the first read. */
  zones: MyZone[];
  /** The zones' files kept as they are. */
  kept: KeptFile[];
  /** Reads my zones again; a failure is said in a notification. */
  refresh: () => Promise<void>;
  /** A new zone, "Nouvelle zone" in the player's language, on `world`; its id, or `null` when it was not made. */
  create: (world: number) => Promise<string | null>;
  /** Names the zone `id` `name`; a blank name is left as it was. */
  rename: (id: string, name: string) => Promise<void>;
  /** Adds `maps` to the zone `id`, or takes them away, shown at once and saved; a failure reads the zones again. */
  paint: (id: string, maps: readonly (readonly [number, number])[], erase: boolean) => Promise<void>;
  /** Sends the zone `id` to the system's trash. */
  remove: (id: string) => Promise<boolean>;
}

/** What a failed change of a zone says, in the player's language. */
function failure(error: unknown): string {
  const failed = error as ZoneError;
  switch (typeof failed?.kind === "string" ? failed.kind : null) {
    case "noSuchZone":
      return i18next.t("zone.failed.noSuchZone");
    case "noFolder":
      return i18next.t("zone.failed.noFolder");
    case "emptyName":
      return i18next.t("zone.failed.emptyName");
    case "notSaved":
      return i18next.t("zone.failed.notSaved", { reason: (failed as { reason: string }).reason });
    default:
      return String(error);
  }
}

/** `zone` with `maps` added or taken away, each once, in order, as Rust keeps them. */
export function stroked(zone: MyZone, maps: readonly (readonly [number, number])[], erase: boolean): MyZone {
  const held = new Map(zone.maps.map((map) => [`${map[0]},${map[1]}`, map]));
  for (const [x, y] of maps) {
    if (erase) {
      held.delete(`${x},${y}`);
    } else {
      held.set(`${x},${y}`, [x, y]);
    }
  }
  const ordered = [...held.values()].sort((a, b) => a[0] - b[0] || a[1] - b[1]);
  return { ...zone, maps: ordered };
}

// Each read and each zone's strokes are numbered: an answer older than one already asked for is dropped.
let reads = 0;
const strokes = new Map<string, number>();

export const useZones = create<ZonesStore>((set, get) => {
  const replace = (zone: MyZone) => set({ zones: get().zones.map((one) => (one.id === zone.id ? zone : one)) });
  return {
    zones: [],
    kept: [],

    refresh: async () => {
      const number = ++reads;
      try {
        const list = await commands.zones();
        if (number === reads) {
          set({ zones: list.zones, kept: list.kept });
        }
      } catch (error) {
        toast.error(i18next.t("zone.unread", { reason: failure(error) }));
      }
    },

    create: async (world) => {
      try {
        const zone = await commands.createZone(i18next.t("zone.newName"), world);
        // A read asked before the zone was made would list it without the zone: dropped.
        reads += 1;
        set({ zones: [...get().zones, zone].sort((a, b) => a.id.localeCompare(b.id)) });
        return zone.id;
      } catch (error) {
        toast.error(failure(error));
        return null;
      }
    },

    rename: async (id, name) => {
      if (name.trim() === "") {
        return;
      }
      try {
        replace(await commands.renameZone(id, name));
      } catch (error) {
        toast.error(failure(error));
        await get().refresh();
      }
    },

    paint: async (id, maps, erase) => {
      const zone = get().zones.find((one) => one.id === id);
      if (zone === undefined || maps.length === 0) {
        return;
      }
      const number = (strokes.get(id) ?? 0) + 1;
      strokes.set(id, number);
      replace(stroked(zone, maps, erase));
      try {
        const saved = await commands.paintZone(id, { maps: maps.map(([x, y]) => [x, y]), erase });
        if (strokes.get(id) === number) {
          replace(saved);
        }
      } catch (error) {
        toast.error(failure(error));
        await get().refresh();
      }
    },

    remove: async (id) => {
      try {
        await commands.deleteZone(id);
        set({ zones: get().zones.filter((one) => one.id !== id) });
        return true;
      } catch (error) {
        toast.error(failure(error));
        return false;
      }
    },
  };
});
