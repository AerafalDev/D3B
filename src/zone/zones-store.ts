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
  /** Whether my zones were read once: before, a zone not listed is not yet known rather than gone. */
  loaded: boolean;
  /** Reads my zones again; a failure is said in a notification. */
  refresh: () => Promise<void>;
  /** A new zone, "Nouvelle zone" in the player's language, on `world`; its id, or `null` when it was not made. */
  create: (world: number) => Promise<string | null>;
  /** Names the zone `id` `name`; a blank name is left as it was; whether it was named. */
  rename: (id: string, name: string) => Promise<boolean>;
  /** Adds `maps` to the zone `id`, or takes them away, shown at once and saved; a failure reads the zones again. */
  paint: (id: string, maps: readonly (readonly [number, number])[], erase: boolean) => Promise<void>;
  /** Sends the zone `id` to the system's trash; whether it went. */
  remove: (id: string) => Promise<boolean>;
}

/** The words of a rejection that is no `ZoneError`, a bug said as it came. */
function said(error: unknown): string {
  return error instanceof Error ? error.message : typeof error === "string" ? error : JSON.stringify(error);
}

/** A rejection as a `ZoneError`, when it is one. */
function zoneError(error: unknown): ZoneError | null {
  const failed = error as ZoneError | null;
  return typeof failed?.kind === "string" ? failed : null;
}

/** What a failed change of a zone says, in the player's language. */
function failure(error: unknown): string {
  const failed = zoneError(error);
  switch (failed?.kind) {
    case "noSuchZone":
      return i18next.t("zone.failed.noSuchZone");
    case "noFolder":
      return i18next.t("zone.failed.noFolder");
    case "emptyName":
      return i18next.t("zone.failed.emptyName");
    case "notSaved":
      return i18next.t("zone.failed.notSaved", { reason: failed.reason });
    case undefined:
      return said(error);
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

// Each read and each zone's strokes are numbered: an answer older than one already asked for is dropped, and so is a
// read asked before a change, which would bring back what the change replaced.
let reads = 0;
const strokes = new Map<string, number>();

export const useZones = create<ZonesStore>((set, get) => {
  // A change shown here at once: the reads under way are older than it.
  const changed = (zones: MyZone[]) => {
    reads += 1;
    set({ zones });
  };
  const replace = (zone: MyZone) => changed(get().zones.map((one) => (one.id === zone.id ? zone : one)));
  return {
    zones: [],
    kept: [],
    loaded: false,

    refresh: async () => {
      const number = ++reads;
      try {
        const list = await commands.zones();
        if (number === reads) {
          set({ zones: list.zones, kept: list.kept, loaded: true });
        }
      } catch (error) {
        const failed = zoneError(error);
        const reason = failed?.kind === "notSaved" ? failed.reason : failed === null ? said(error) : failure(error);
        toast.error(i18next.t("zone.unread", { reason }));
      }
    },

    create: async (world) => {
      try {
        const zone = await commands.createZone(i18next.t("zone.newName"), world);
        changed([...get().zones, zone].sort((a, b) => a.id.localeCompare(b.id)));
        // The zones a read dropped by it lists come back.
        void get().refresh();
        return zone.id;
      } catch (error) {
        toast.error(failure(error));
        return null;
      }
    },

    rename: async (id, name) => {
      if (name.trim() === "") {
        return false;
      }
      try {
        replace(await commands.renameZone(id, name));
        return true;
      } catch (error) {
        toast.error(failure(error));
        await get().refresh();
        return false;
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
        changed(get().zones.filter((one) => one.id !== id));
        return true;
      } catch (error) {
        toast.error(failure(error));
        return false;
      }
    },
  };
});
