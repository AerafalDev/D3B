import { commands } from "@/bindings.ts";
import { named } from "@/characters/names.ts";
import { valueText } from "@/library/value-text.ts";
import { useWorld } from "@/world/world-store.ts";
import { folded } from "@/world/world-tree.ts";
import { useZones } from "@/zone/zones-store.ts";
import { parseDuration } from "./duration.ts";

const YES = ["oui", "yes", "vrai", "true"];
const NO = ["non", "no", "faux", "false"];

/**
 * A value of `valueType` from the words the player types for it (the owner, 2026-11-17: "1 min 30", "23:00", "5,-18",
 * a sub-area's, zone's, profession's or item's name, an id); `null` for words that name none. A list's values are
 * separated by ";".
 */
export async function fromWords(valueType: string, list: boolean, words: string): Promise<unknown> {
  if (list) {
    const values = await Promise.all(words.split(";").map((one) => fromWords(valueType, false, one)));
    return values.includes(null) ? null : values;
  }
  const text = words.trim();
  const sought = folded(text);
  const whole = /^\d+$/.test(text) ? Number(text) : null;
  switch (valueType) {
    case "number":
      return /^-?\d+$/.test(text) ? Number(text) : null;
    case "percentage":
      return whole !== null && whole <= 100 ? whole : null;
    case "bool":
      return YES.includes(sought) ? true : NO.includes(sought) ? false : null;
    case "text":
      return text;
    case "duration":
      return parseDuration(text);
    case "time":
      return /^([01]\d|2[0-3]):[0-5]\d$/.test(text) ? text : null;
    case "job": {
      const read = useWorld.getState().read;
      const jobs = read?.state === "read" ? read.atlas.jobs : [];
      const found = jobs.find(
        (job) => job.id === whole || [job.name.fr, job.name.en].some((n) => folded(n) === sought),
      );
      return found?.id ?? null;
    }
    case "zone": {
      const mine = useZones.getState().zones.find((zone) => folded(zone.name) === sought);
      if (mine !== undefined) {
        return { zone: mine.id };
      }
      const read = useWorld.getState().read;
      const subAreas = read?.state === "read" ? read.atlas.areas.flatMap((area) => area.subAreas) : [];
      const found = subAreas.find(
        (one) => one.id === whole || [one.name.fr, one.name.en].some((n) => folded(n) === sought),
      );
      return found === undefined ? null : { sub_area: found.id };
    }
    case "map": {
      const at = /^\s*(-?\d+)\s*,\s*(-?\d+)\s*$/.exec(text);
      if (at === null) {
        return whole;
      }
      // On the world the World tab shows, as the map's field reads them.
      const map = await commands.mapAt(useWorld.getState().world, Number(at[1]), Number(at[2])).catch(() => null);
      return map?.id ?? null;
    }
    case "item": {
      if (whole !== null) {
        return whole;
      }
      const found = await commands.findItems(text, []).catch(() => []);
      const exact = found.find((item) => [item.name.fr, item.name.en].some((n) => folded(n) === sought));
      // A name not found whole names nothing: a paste never takes another item silently.
      return exact?.id ?? null;
    }
    default:
      // A bank, items, a route, an NPC: their file's JSON, until their editors.
      try {
        return JSON.parse(text);
      } catch {
        return null;
      }
  }
}

/** A profession's, or a zone of mine's or a sub-area's, name as its editor shows it; `null` for another type. */
export function nameOf(valueType: string, value: unknown, language: string): string | null {
  switch (valueType) {
    case "job": {
      const id = typeof value === "number" ? value : null;
      const read = useWorld.getState().read;
      const job = read?.state === "read" ? read.atlas.jobs.find((one) => one.id === id) : undefined;
      return named(job?.name, language) ?? String(id);
    }
    case "zone": {
      const zone = value as { sub_area?: number; zone?: string };
      const mine = useZones.getState().zones.find((one) => one.id === zone.zone);
      if (mine !== undefined) {
        return mine.name;
      }
      const read = useWorld.getState().read;
      const subAreas = read?.state === "read" ? read.atlas.areas.flatMap((area) => area.subAreas) : [];
      const found = subAreas.find((one) => one.id === zone.sub_area);
      return named(found?.name, language) ?? String(zone.sub_area ?? zone.zone);
    }
    default:
      return null;
  }
}

/** The words `value` of `valueType` is typed as, which `fromWords` reads back the same. */
export async function toWords(valueType: string, list: boolean, value: unknown, language: string): Promise<string> {
  if (list && Array.isArray(value)) {
    const words = await Promise.all(value.map((one) => toWords(valueType, false, one, language)));
    return words.join("; ");
  }
  const id = typeof value === "number" ? value : null;
  switch (valueType) {
    case "number":
    case "percentage":
    case "text":
    case "time":
      return String(value);
    case "bool":
      return language.startsWith("fr") ? (value === true ? "oui" : "non") : value === true ? "yes" : "no";
    case "duration":
      return valueText("duration", JSON.stringify(value), language);
    case "job":
    case "zone":
      return nameOf(valueType, value, language) ?? JSON.stringify(value);
    case "map": {
      const found = id === null ? [] : await commands.findMaps([id]).catch(() => []);
      const map = found[0];
      // Coordinates read back the same map only on the world the World tab shows, and for the map drawn there: an
      // inside, or a map of another world, is written by its id.
      const back =
        map === undefined
          ? null
          : await commands.mapAt(useWorld.getState().world, map.at[0], map.at[1]).catch(() => null);
      return map === undefined || back?.id !== map.id ? String(id) : `${map.at[0]},${map.at[1]}`;
    }
    case "item": {
      const found = id === null ? [] : await commands.findItems("", [id]).catch(() => []);
      return named(found[0]?.name, language) ?? String(id);
    }
    default:
      return JSON.stringify(value);
  }
}
